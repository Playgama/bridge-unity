using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor.Tabs
{
    public sealed class PlayerPrefsTab : ITab
    {
        public string TabName => "PlayerPrefs";

        private sealed class Entry
        {
            public string Key;
            public PlayerPrefType Type;
            public string StringValue;
            public int IntValue;
            public float FloatValue;
            public bool IsEditing;
            public string EditBuffer;
            public PlayerPrefType EditType;
        }

        [Serializable]
        private sealed class SerializedPrefEntry
        {
            public string key;
            public string type;
            public string value;
        }

        [Serializable]
        private sealed class SerializedPrefs
        {
            public List<SerializedPrefEntry> entries = new List<SerializedPrefEntry>();
        }

        private enum SortColumn { Key, Type, Value }

        private BuildInfo _buildInfo;
        private readonly List<Entry> _entries = new List<Entry>();
        private Vector2 _scroll;
        private Vector2 _listScroll;
        private string _status = "";
        private string _search = "";

        private bool _foldHeader;
        private bool _foldAddNew = true;
        private bool _foldList = true;

        private string _newKey = "";
        private string _newValue = "";
        private PlayerPrefType _newType = PlayerPrefType.String;

        private Entry _pendingDelete;

        private SortColumn _sortBy = SortColumn.Key;
        private bool _sortAscending = true;

        private const long MaxImportFileSizeBytes = 10 * 1024 * 1024;
        private const int MaxImportEntryCount = 10000;

        private static GUIStyle _rowLabelStyle;
        private static GUIStyle _rowLabelTruncatedStyle;
        private static GUIStyle _miniButtonStyle;
        private static GUIStyle _sortHeaderStyle;

        [UnityEditor.InitializeOnLoadMethod]
        private static void ResetStyles()
        {
            _rowLabelStyle = null;
        }

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;

            if (_entries.Count == 0)
                LoadEntries();
        }

        public void OnGUI()
        {
            EnsureStyles();

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawHeader();
                GUILayout.Space(4);
                DrawToolbar();
                GUILayout.Space(4);
                DrawAddNewSection();
                GUILayout.Space(4);
                DrawList();

                if (_pendingDelete != null)
                {
                    if (EditorUtility.DisplayDialog("Delete PlayerPref",
                            $"Are you sure you want to delete \"{_pendingDelete.Key}\"?",
                            "Delete", "Cancel"))
                    {
                        PlayerPrefs.DeleteKey(_pendingDelete.Key);
                        PlayerPrefs.Save();
                        _entries.Remove(_pendingDelete);
                        _status = $"Deleted \"{_pendingDelete.Key}\".";
                    }
                    _pendingDelete = null;
                }

                if (!string.IsNullOrEmpty(_status))
                {
                    GUILayout.Space(4);
                    EditorGUILayout.HelpBox(_status, MessageType.None);
                }
            }
        }

        private void DrawHeader()
        {
            _foldHeader = BridgeStyles.DrawSectionHeader("About PlayerPrefs", _foldHeader, "\u2139");
            if (!_foldHeader) return;

            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("View, edit, add, and delete Unity PlayerPrefs for this project.", BridgeStyles.subtitleStyle);
            GUILayout.Space(4);

            var storagePath = PlayerPrefsReader.GetStoragePath();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Storage:", GUILayout.Width(60));
                EditorGUILayout.SelectableLabel(storagePath, EditorStyles.miniLabel, GUILayout.Height(16));
            }

            BridgeStyles.EndCard();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    LoadEntries();
                    _status = $"Refreshed. Found {_entries.Count} entries.";
                }

                GUILayout.Space(4);

                if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    ExportEntries();

                if (GUILayout.Button("Import", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    ImportEntries();

                GUILayout.Space(8);
                GUILayout.Label("Search:", GUILayout.Width(48));
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);

                if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
                    _search = "";

                GUILayout.FlexibleSpace();

                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = BridgeStyles.statusRed;
                if (GUILayout.Button("Delete All", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    DeleteAll();
                GUI.backgroundColor = oldBg;
            }
        }

        private void DrawAddNewSection()
        {
            _foldAddNew = BridgeStyles.DrawSectionHeader("Add New Entry", _foldAddNew, "+");
            if (!_foldAddNew) return;

            BridgeStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Key:", GUILayout.Width(32));
                _newKey = EditorGUILayout.TextField(_newKey, GUILayout.MinWidth(100));

                GUILayout.Space(8);
                GUILayout.Label("Type:", GUILayout.Width(36));
                _newType = (PlayerPrefType)EditorGUILayout.EnumPopup(_newType, GUILayout.Width(65));

                GUILayout.Space(8);
                GUILayout.Label("Value:", GUILayout.Width(40));
                _newValue = EditorGUILayout.TextField(_newValue, GUILayout.MinWidth(80));

                GUILayout.Space(8);
                if (BridgeStyles.DrawAccentButton("Add", GUILayout.Width(50)))
                    AddNewEntry();
            }
            BridgeStyles.EndCard();
        }

        private void DrawList()
        {
            int visibleCount = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (PassSearch(_entries[i].Key, _search))
                    visibleCount++;
            }

            var title = _entries.Count > 0
                ? $"PlayerPrefs ({visibleCount}/{_entries.Count})"
                : "PlayerPrefs";

            _foldList = BridgeStyles.DrawSectionHeader(title, _foldList, "\u2630");
            if (!_foldList) return;

            if (_entries.Count == 0)
            {
                BridgeStyles.BeginCard();
                EditorGUILayout.HelpBox("No PlayerPrefs found for this project. Use \"Add New Entry\" above or set prefs via script and click Refresh.", MessageType.Info);
                BridgeStyles.EndCard();
                return;
            }

            DrawColumnHeaders();

            using (var sv = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.MinHeight(200), GUILayout.MaxHeight(500)))
            {
                _listScroll = sv.scrollPosition;

                int rowIndex = 0;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (!PassSearch(_entries[i].Key, _search))
                        continue;

                    DrawRow(_entries[i], rowIndex);
                    rowIndex++;
                }
            }
        }

        private void DrawColumnHeaders()
        {
            var rect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(rect, BridgeStyles.darkBackground);

            float x = rect.x + 4;
            float keyW = rect.width * 0.35f;
            float typeW = 60;
            float actionsW = 110;
            float valueW = rect.width - keyW - typeW - actionsW - 16;

            if (GUI.Button(new Rect(x, rect.y, keyW, rect.height), GetSortLabel("Key", SortColumn.Key), _sortHeaderStyle))
                ToggleSort(SortColumn.Key);

            if (GUI.Button(new Rect(x + keyW + 4, rect.y, typeW, rect.height), GetSortLabel("Type", SortColumn.Type), _sortHeaderStyle))
                ToggleSort(SortColumn.Type);

            if (GUI.Button(new Rect(x + keyW + typeW + 8, rect.y, valueW, rect.height), GetSortLabel("Value", SortColumn.Value), _sortHeaderStyle))
                ToggleSort(SortColumn.Value);

            GUI.Label(new Rect(rect.xMax - actionsW - 4, rect.y, actionsW, rect.height), "Actions", _sortHeaderStyle);
        }

        private void DrawRow(Entry entry, int index)
        {
            var rect = EditorGUILayout.GetControlRect(false, 26);
            BridgeStyles.DrawListRowBackground(rect, index, BridgeStyles.cardBackground);

            float x = rect.x + 4;
            float keyW = rect.width * 0.35f;
            float typeW = 60;
            float actionsW = 110;
            float valueW = rect.width - keyW - typeW - actionsW - 16;

            float y = rect.y + 2;
            float h = rect.height - 4;

            var keyText = entry.Key.Length > 40 ? entry.Key.Substring(0, 37) + "..." : entry.Key;
            GUI.Label(new Rect(x, y, keyW, h), new GUIContent(keyText, entry.Key), _rowLabelTruncatedStyle);

            if (entry.IsEditing)
            {
                entry.EditType = (PlayerPrefType)EditorGUI.EnumPopup(new Rect(x + keyW + 4, y, typeW, h), entry.EditType);
                entry.EditBuffer = EditorGUI.TextField(new Rect(x + keyW + typeW + 8, y, valueW, h), entry.EditBuffer);

                float btnX = rect.xMax - actionsW - 4;
                if (GUI.Button(new Rect(btnX, y, 50, h), "Save", _miniButtonStyle))
                    SaveEntry(entry);

                if (GUI.Button(new Rect(btnX + 54, y, 50, h), "Cancel", _miniButtonStyle))
                    entry.IsEditing = false;
            }
            else
            {
                var typeRect = new Rect(x + keyW + 4, y + 2, typeW, h - 4);
                BridgeStyles.DrawTag(typeRect, entry.Type.ToString(), GetTypeColor(entry.Type));

                var displayValue = GetDisplayValue(entry);
                var truncated = displayValue.Length > 60 ? displayValue.Substring(0, 57) + "..." : displayValue;
                GUI.Label(new Rect(x + keyW + typeW + 8, y, valueW, h), new GUIContent(truncated, displayValue), _rowLabelStyle);

                float btnX = rect.xMax - actionsW - 4;
                if (GUI.Button(new Rect(btnX, y, 50, h), "Edit", _miniButtonStyle))
                {
                    entry.IsEditing = true;
                    entry.EditBuffer = GetDisplayValue(entry);
                    entry.EditType = entry.Type;
                }

                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                if (GUI.Button(new Rect(btnX + 54, y, 50, h), "Delete", _miniButtonStyle))
                    _pendingDelete = entry;
                GUI.backgroundColor = oldBg;
            }
        }

        private void LoadEntries()
        {
            _entries.Clear();

            try
            {
                var rawEntries = PlayerPrefsReader.ReadAllEntries();

                var seen = new HashSet<string>();
                for (int i = 0; i < rawEntries.Count; i++)
                {
                    var raw = rawEntries[i];
                    if (!seen.Add(raw.Key))
                        continue;

                    if (!PlayerPrefs.HasKey(raw.Key))
                        continue;

                    var entry = new Entry
                    {
                        Key = raw.Key,
                        Type = raw.Type
                    };

                    ReadValue(entry);
                    _entries.Add(entry);
                }
            }
            catch (Exception e)
            {
                _status = $"Error loading PlayerPrefs: {e.Message}";
            }

            SortEntries();
        }

        private static void ReadValue(Entry entry)
        {
            switch (entry.Type)
            {
                case PlayerPrefType.Int:
                    entry.IntValue = PlayerPrefs.GetInt(entry.Key, 0);
                    entry.StringValue = entry.IntValue.ToString();
                    break;
                case PlayerPrefType.Float:
                    entry.FloatValue = PlayerPrefs.GetFloat(entry.Key, 0f);
                    entry.StringValue = entry.FloatValue.ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    entry.StringValue = PlayerPrefs.GetString(entry.Key, "");
                    break;
            }
        }

        private void SaveEntry(Entry entry)
        {
            var buffer = entry.EditBuffer ?? "";

            switch (entry.EditType)
            {
                case PlayerPrefType.Int:
                    if (!int.TryParse(buffer, out var intVal))
                    {
                        _status = $"Invalid integer value: \"{buffer}\"";
                        return;
                    }
                    if (entry.Type != entry.EditType)
                        PlayerPrefs.DeleteKey(entry.Key);
                    PlayerPrefs.SetInt(entry.Key, intVal);
                    entry.IntValue = intVal;
                    entry.StringValue = intVal.ToString();
                    break;

                case PlayerPrefType.Float:
                    if (!float.TryParse(buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal))
                    {
                        _status = $"Invalid float value: \"{buffer}\"";
                        return;
                    }
                    if (entry.Type != entry.EditType)
                        PlayerPrefs.DeleteKey(entry.Key);
                    PlayerPrefs.SetFloat(entry.Key, floatVal);
                    entry.FloatValue = floatVal;
                    entry.StringValue = floatVal.ToString(CultureInfo.InvariantCulture);
                    break;

                default:
                    if (entry.Type != entry.EditType)
                        PlayerPrefs.DeleteKey(entry.Key);
                    PlayerPrefs.SetString(entry.Key, buffer);
                    entry.StringValue = buffer;
                    break;
            }

            entry.Type = entry.EditType;
            entry.IsEditing = false;
            PlayerPrefs.Save();
            _status = $"Saved \"{entry.Key}\" as {entry.Type}.";
        }

        private void DeleteAll()
        {
            if (!EditorUtility.DisplayDialog("Delete All PlayerPrefs",
                    $"This will permanently delete ALL {_entries.Count} PlayerPrefs for this project.\n\nThis cannot be undone.",
                    "Delete All", "Cancel"))
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            _entries.Clear();
            _status = "All PlayerPrefs deleted.";
        }

        private void ExportEntries()
        {
            var toExport = new List<Entry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (PassSearch(_entries[i].Key, _search))
                    toExport.Add(_entries[i]);
            }

            if (toExport.Count == 0)
            {
                _status = "Nothing to export.";
                return;
            }

            var defaultName = $"PlayerPrefs_{PlayerSettings.productName}";
            var path = EditorUtility.SaveFilePanel("Export PlayerPrefs", "", defaultName, "json");
            if (string.IsNullOrEmpty(path))
                return;

            var data = new SerializedPrefs();
            for (int i = 0; i < toExport.Count; i++)
            {
                data.entries.Add(new SerializedPrefEntry
                {
                    key = toExport[i].Key,
                    type = toExport[i].Type.ToString(),
                    value = GetDisplayValue(toExport[i])
                });
            }

            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);

            var filterNote = !string.IsNullOrEmpty(_search) && toExport.Count < _entries.Count
                ? $" (filtered by \"{_search}\", {_entries.Count} total)"
                : "";
            _status = $"Exported {toExport.Count} entries to {Path.GetFileName(path)}.{filterNote}";
        }

        private void ImportEntries()
        {
            var path = EditorUtility.OpenFilePanel("Import PlayerPrefs", "", "json");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var fileSize = new FileInfo(path).Length;
                if (fileSize > MaxImportFileSizeBytes)
                {
                    var sizeMB = fileSize / (1024.0 * 1024.0);
                    var maxMB = MaxImportFileSizeBytes / (1024 * 1024);
                    _status = $"File too large ({sizeMB:F1} MB). Maximum is {maxMB} MB.";
                    return;
                }
            }
            catch (Exception e)
            {
                _status = $"Failed to check file: {e.Message}";
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                _status = $"Failed to read file: {e.Message}";
                return;
            }

            SerializedPrefs data;
            try
            {
                data = JsonUtility.FromJson<SerializedPrefs>(json);
            }
            catch (Exception e)
            {
                _status = $"Invalid JSON format: {e.Message}";
                return;
            }

            if (data == null || data.entries == null || data.entries.Count == 0)
            {
                _status = "File contains no entries.";
                return;
            }

            if (data.entries.Count > MaxImportEntryCount)
            {
                _status = $"Too many entries ({data.entries.Count}). Maximum is {MaxImportEntryCount}.";
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Import PlayerPrefs",
                $"Importing {data.entries.Count} entries from {Path.GetFileName(path)}.\n\nChoose import mode:",
                "Merge (add & overwrite)",
                "Cancel",
                "Replace (delete all first)");

            if (choice == 1)
                return;

            if (choice == 2)
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }

            int imported = 0;
            int failed = 0;
            var importedEntries = new List<Entry>();

            for (int i = 0; i < data.entries.Count; i++)
            {
                var e = data.entries[i];
                if (string.IsNullOrEmpty(e.key))
                {
                    failed++;
                    continue;
                }

                PlayerPrefType type;
                try
                {
                    type = (PlayerPrefType)Enum.Parse(typeof(PlayerPrefType), e.type, true);
                }
                catch
                {
                    type = PlayerPrefType.String;
                }

                // Clear old typed data before writing a potentially different type
                if (PlayerPrefs.HasKey(e.key))
                    PlayerPrefs.DeleteKey(e.key);

                switch (type)
                {
                    case PlayerPrefType.Int:
                        if (int.TryParse(e.value, out var intVal))
                        {
                            PlayerPrefs.SetInt(e.key, intVal);
                            importedEntries.Add(new Entry
                            {
                                Key = e.key, Type = PlayerPrefType.Int,
                                IntValue = intVal, StringValue = intVal.ToString()
                            });
                            imported++;
                        }
                        else
                            failed++;
                        break;

                    case PlayerPrefType.Float:
                        if (float.TryParse(e.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal))
                        {
                            PlayerPrefs.SetFloat(e.key, floatVal);
                            importedEntries.Add(new Entry
                            {
                                Key = e.key, Type = PlayerPrefType.Float,
                                FloatValue = floatVal,
                                StringValue = floatVal.ToString(CultureInfo.InvariantCulture)
                            });
                            imported++;
                        }
                        else
                            failed++;
                        break;

                    default:
                        PlayerPrefs.SetString(e.key, e.value ?? "");
                        importedEntries.Add(new Entry
                        {
                            Key = e.key, Type = PlayerPrefType.String,
                            StringValue = e.value ?? ""
                        });
                        imported++;
                        break;
                }
            }

            PlayerPrefs.Save();

            // Merge into the in-memory list directly — PlayerPrefs.Save() may not
            // have flushed to the backing store yet when LoadEntries reads it
            if (choice == 2)
                _entries.Clear();

            var existingKeys = new HashSet<string>();
            for (int i = 0; i < _entries.Count; i++)
                existingKeys.Add(_entries[i].Key);

            for (int i = 0; i < importedEntries.Count; i++)
            {
                var entry = importedEntries[i];
                if (existingKeys.Contains(entry.Key))
                {
                    for (int j = 0; j < _entries.Count; j++)
                    {
                        if (_entries[j].Key == entry.Key)
                        {
                            _entries[j].Type = entry.Type;
                            _entries[j].IntValue = entry.IntValue;
                            _entries[j].FloatValue = entry.FloatValue;
                            _entries[j].StringValue = entry.StringValue;
                            _entries[j].IsEditing = false;
                            break;
                        }
                    }
                }
                else
                {
                    _entries.Add(entry);
                    existingKeys.Add(entry.Key);
                }
            }

            SortEntries();

            _status = failed > 0
                ? $"Imported {imported} entries ({failed} failed) from {Path.GetFileName(path)}."
                : $"Imported {imported} entries from {Path.GetFileName(path)}.";
        }

        private void AddNewEntry()
        {
            if (string.IsNullOrEmpty(_newKey))
            {
                _status = "Key cannot be empty.";
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Key == _newKey)
                {
                    _status = $"Key \"{_newKey}\" already exists. Edit the existing entry instead.";
                    return;
                }
            }

            var entry = new Entry { Key = _newKey, Type = _newType };

            switch (_newType)
            {
                case PlayerPrefType.Int:
                    if (!int.TryParse(_newValue, out var intVal))
                    {
                        _status = $"Invalid integer value: \"{_newValue}\"";
                        return;
                    }
                    PlayerPrefs.SetInt(_newKey, intVal);
                    entry.IntValue = intVal;
                    entry.StringValue = intVal.ToString();
                    break;

                case PlayerPrefType.Float:
                    if (!float.TryParse(_newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal))
                    {
                        _status = $"Invalid float value: \"{_newValue}\"";
                        return;
                    }
                    PlayerPrefs.SetFloat(_newKey, floatVal);
                    entry.FloatValue = floatVal;
                    entry.StringValue = floatVal.ToString(CultureInfo.InvariantCulture);
                    break;

                default:
                    PlayerPrefs.SetString(_newKey, _newValue);
                    entry.StringValue = _newValue;
                    break;
            }

            PlayerPrefs.Save();
            _entries.Add(entry);
            SortEntries();

            _status = $"Added \"{_newKey}\" ({_newType}).";
            _newKey = "";
            _newValue = "";
        }

        private static bool PassSearch(string key, string search)
        {
            if (string.IsNullOrEmpty(search))
                return true;
            return key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetDisplayValue(Entry entry)
        {
            return entry.StringValue ?? "";
        }

        private static Color GetTypeColor(PlayerPrefType type)
        {
            switch (type)
            {
                case PlayerPrefType.Int: return BridgeStyles.statusGreen;
                case PlayerPrefType.Float: return BridgeStyles.statusYellow;
                default: return BridgeStyles.brandPurpleDark;
            }
        }

        private string GetSortLabel(string name, SortColumn col)
        {
            if (_sortBy != col)
                return name;
            return _sortAscending ? name + " \u25B2" : name + " \u25BC";
        }

        private void ToggleSort(SortColumn col)
        {
            if (_sortBy == col)
                _sortAscending = !_sortAscending;
            else
            {
                _sortBy = col;
                _sortAscending = true;
            }
            SortEntries();
        }

        private void SortEntries()
        {
            _entries.Sort((a, b) =>
            {
                int cmp;
                switch (_sortBy)
                {
                    case SortColumn.Type:
                        cmp = a.Type.CompareTo(b.Type);
                        if (cmp == 0) cmp = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                        break;
                    case SortColumn.Value:
                        cmp = string.Compare(GetDisplayValue(a), GetDisplayValue(b), StringComparison.OrdinalIgnoreCase);
                        if (cmp == 0) cmp = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                        break;
                    default:
                        cmp = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                        break;
                }
                return _sortAscending ? cmp : -cmp;
            });
        }

        private static void EnsureStyles()
        {
            if (_rowLabelStyle != null)
                return;

            _rowLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                padding = new RectOffset(2, 2, 2, 2)
            };

            _rowLabelTruncatedStyle = new GUIStyle(_rowLabelStyle)
            {
                fontStyle = FontStyle.Bold
            };

            _miniButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2)
            };

            var headerTextColor = EditorGUIUtility.isProSkin
                ? new Color(0.8f, 0.8f, 0.8f)
                : new Color(0.2f, 0.2f, 0.2f);

            _sortHeaderStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 4, 2, 2),
                normal = { textColor = headerTextColor }
            };
        }
    }
}
