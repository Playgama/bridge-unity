using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Playgama.Bridge
{
    /// <summary>
    /// Tinify (TinyPNG) helper for editor-side PNG/JPG optimization.
    /// </summary>
    public static class TinifyUtility
    {
        private const string PrefKey = "SUIT_TINIFY_API_KEY";
        private const string ShrinkUrl = "https://api.tinify.com/shrink";

        public sealed class BatchResult
        {
            public int Total;
            public int Optimized;
            public int Skipped;
            public int Failed;
            public long BytesBefore;
            public long BytesAfter;
            public readonly List<string> Errors = new List<string>(64);
            public long BytesSaved => Math.Max(0, BytesBefore - BytesAfter);
        }

        private sealed class Job
        {
            public string AssetPath;
            public string AbsolutePath;
            public long BeforeBytes;
            public long AfterBytes;
            public Stage Stage;
            public UnityWebRequest Request;
            public string LocationUrl;
            public bool Done;
            public bool Failed;
            public string Error;
        }

        private enum Stage { None, PostShrink, GetResult }

        private static bool _running;
        private static string _apiKey;
        private static Queue<Job> _queue;
        private static Job _current;
        private static Action<int, int, string> _onProgress;
        private static Action<BatchResult> _onDone;
        private static BatchResult _result;

        public static string GetKey() => EditorPrefs.GetString(PrefKey, string.Empty);
        public static void SetKey(string key) => EditorPrefs.SetString(PrefKey, key ?? string.Empty);
        public static void ClearKey() => EditorPrefs.DeleteKey(PrefKey);
        public static bool HasKey() => !string.IsNullOrEmpty(GetKey());

        public static bool IsSupportedExtension(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        public static bool CanUseTinify(out string reason)
        {
            if (!HasKey())
            {
                reason = "Tinify API key is not set. Go to Settings tab and paste your key.";
                return false;
            }
            reason = null;
            return true;
        }

        public static void ValidateKeyAsync(Action<bool, string> onComplete)
        {
            string key = GetKey();
            if (string.IsNullOrEmpty(key))
            {
                onComplete?.Invoke(false, "Key is empty.");
                return;
            }

            byte[] tinyPng = GetTinyPngBytes();

            var req = new UnityWebRequest(ShrinkUrl, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(tinyPng);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/octet-stream");
            ApplyAuth(req, key);

            var op = req.SendWebRequest();

            void Tick()
            {
                if (!op.isDone) return;
                EditorApplication.update -= Tick;

                try
                {
                    long code = req.responseCode;
                    bool hadTransportError = HasTransportError(req);
                    if (hadTransportError)
                    {
                        onComplete?.Invoke(false, "Network error: " + req.error);
                        return;
                    }
                    if (code == 401)
                    {
                        onComplete?.Invoke(false, "Invalid key (401 Unauthorized).");
                        return;
                    }
                    onComplete?.Invoke(true, $"Key looks valid. HTTP {code}.");
                }
                catch (Exception ex)
                {
                    onComplete?.Invoke(false, "Validation exception: " + ex.Message);
                }
                finally
                {
                    req.Dispose();
                }
            }

            EditorApplication.update += Tick;
        }

        public static void OptimizeAssetsAsync(
            List<string> assetPaths,
            Action<int, int, string> onProgress,
            Action<BatchResult> onDone)
        {
            if (_running)
            {
                onDone?.Invoke(new BatchResult
                {
                    Total = assetPaths != null ? assetPaths.Count : 0,
                    Failed = 1,
                    Errors = { "Tinify batch is already running." }
                });
                return;
            }

            if (assetPaths == null || assetPaths.Count == 0)
            {
                onDone?.Invoke(new BatchResult());
                return;
            }

            if (!CanUseTinify(out string reason))
            {
                var r = new BatchResult { Total = assetPaths.Count, Failed = assetPaths.Count };
                r.Errors.Add(reason);
                onDone?.Invoke(r);
                return;
            }

            _apiKey = GetKey();
            _queue = new Queue<Job>(assetPaths.Count);
            _result = new BatchResult { Total = assetPaths.Count };
            _onProgress = onProgress;
            _onDone = onDone;

            string projectRoot = GetProjectRoot();

            for (int i = 0; i < assetPaths.Count; i++)
            {
                string ap = assetPaths[i];
                if (!IsSupportedExtension(ap))
                {
                    _result.Skipped++;
                    continue;
                }

                string abs = ToAbsolutePath(projectRoot, ap);
                if (!File.Exists(abs))
                {
                    _result.Skipped++;
                    _result.Errors.Add($"Missing file: {ap}");
                    continue;
                }

                _queue.Enqueue(new Job
                {
                    AssetPath = ap,
                    AbsolutePath = abs,
                    Stage = Stage.None
                });
            }

            if (_queue.Count == 0)
            {
                _running = false;
                EditorUtility.ClearProgressBar();
                _onDone?.Invoke(_result);
                return;
            }

            _running = true;
            _current = null;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            try
            {
                if (!_running)
                {
                    Stop();
                    return;
                }

                if (_current == null)
                {
                    if (_queue.Count == 0)
                    {
                        Finish();
                        return;
                    }
                    _current = _queue.Dequeue();
                    StartPostShrink(_current);
                    return;
                }

                if (_current.Request == null)
                {
                    FailCurrent("Internal error: Request is null.");
                    return;
                }

                if (!_current.Request.isDone)
                    return;

                HandleRequestDone(_current);
            }
            catch (Exception ex)
            {
                _result.Errors.Add("Tinify update exception: " + ex.Message);
                Finish();
            }
        }

        private static void StartPostShrink(Job j)
        {
            int done = _result.Optimized + _result.Failed + _result.Skipped;
            float p = done / Mathf.Max(1f, (float)_result.Total);

            string msg = "Uploading to Tinify: " + j.AssetPath;
            _onProgress?.Invoke(done, _result.Total, msg);
            EditorUtility.DisplayProgressBar("Playgama Bridge Tinify", msg, p);

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(j.AbsolutePath);
            }
            catch (Exception ex)
            {
                FailCurrent("Read failed: " + ex.Message);
                return;
            }

            j.BeforeBytes = bytes.Length;

            var req = new UnityWebRequest(ShrinkUrl, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/octet-stream");
            ApplyAuth(req, _apiKey);

            j.Stage = Stage.PostShrink;
            j.Request = req;
            req.SendWebRequest();
        }

        private static void StartGetResult(Job j)
        {
            int done = _result.Optimized + _result.Failed + _result.Skipped;
            float p = done / Mathf.Max(1f, (float)_result.Total);

            string msg = "Downloading optimized: " + j.AssetPath;
            _onProgress?.Invoke(done, _result.Total, msg);
            EditorUtility.DisplayProgressBar("Playgama Bridge Tinify", msg, p);

            var req = UnityWebRequest.Get(j.LocationUrl);
            ApplyAuth(req, _apiKey);

            j.Stage = Stage.GetResult;
            j.Request = req;
            req.SendWebRequest();
        }

        private static void HandleRequestDone(Job j)
        {
            long code = j.Request.responseCode;

            if (HasTransportError(j.Request))
            {
                FailCurrent($"Network error: {j.Request.error}");
                return;
            }

            if (code == 401)
            {
                FailCurrent("Invalid Tinify key (401 Unauthorized). Batch aborted.");
                Finish();
                return;
            }

            if (j.Stage == Stage.PostShrink)
            {
                string location = GetHeader(j.Request, "Location");
                if (string.IsNullOrEmpty(location))
                {
                    string body = SafeBody(j.Request);
                    FailCurrent($"Shrink failed. HTTP {code}. {body}");
                    return;
                }

                j.LocationUrl = location;
                j.Request.Dispose();
                j.Request = null;
                StartGetResult(j);
                return;
            }

            if (j.Stage == Stage.GetResult)
            {
                byte[] outBytes = null;
                try
                {
                    outBytes = j.Request.downloadHandler.data;
                }
                catch (Exception ex)
                {
                    FailCurrent("Download handler error: " + ex.Message);
                    return;
                }

                if (outBytes == null || outBytes.Length == 0)
                {
                    FailCurrent($"Download returned empty data. HTTP {code}");
                    return;
                }

                try
                {
                    string temp = j.AbsolutePath + ".suit_tmp";
                    File.WriteAllBytes(temp, outBytes);
                    File.Copy(temp, j.AbsolutePath, true);
                    File.Delete(temp);

                    j.AfterBytes = outBytes.Length;
                    _result.BytesBefore += j.BeforeBytes;
                    _result.BytesAfter += j.AfterBytes;
                    _result.Optimized++;

                    AssetDatabase.ImportAsset(j.AssetPath, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception ex)
                {
                    FailCurrent("Write/replace failed: " + ex.Message);
                    return;
                }
                finally
                {
                    j.Request.Dispose();
                }

                _current = null;

                int done = _result.Optimized + _result.Failed + _result.Skipped;
                float p = done / Mathf.Max(1f, (float)_result.Total);
                EditorUtility.DisplayProgressBar("Playgama Bridge Tinify", "Done: " + done + "/" + _result.Total, p);
                return;
            }

            FailCurrent("Internal error: unknown stage.");
        }

        private static void FailCurrent(string error)
        {
            if (_current == null) return;

            _current.Failed = true;
            _current.Error = error;
            _result.Failed++;
            _result.Errors.Add($"{_current.AssetPath}: {error}");

            try { _current.Request?.Dispose(); } catch { }
            _current = null;

            int done = _result.Optimized + _result.Failed + _result.Skipped;
            float p = done / Mathf.Max(1f, (float)_result.Total);
            EditorUtility.DisplayProgressBar("Playgama Bridge Tinify", "Error: " + done + "/" + _result.Total, p);
        }

        private static void Finish()
        {
            Stop();
            EditorUtility.ClearProgressBar();
            _onDone?.Invoke(_result);
            _result = null;
            _onProgress = null;
            _onDone = null;
        }

        private static void Stop()
        {
            if (!_running) return;
            _running = false;
            EditorApplication.update -= Update;
            try { _current?.Request?.Dispose(); } catch { }
            _current = null;
            _queue = null;
        }

        private static void ApplyAuth(UnityWebRequest req, string key)
        {
            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes("api:" + key));
            req.SetRequestHeader("Authorization", "Basic " + auth);
        }

        private static bool HasTransportError(UnityWebRequest req)
        {
#if UNITY_2020_2_OR_NEWER
            return req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError;
#else
            return req.isNetworkError;
#endif
        }

        private static string GetHeader(UnityWebRequest req, string name)
        {
            try { return req.GetResponseHeader(name); }
            catch { return null; }
        }

        private static string SafeBody(UnityWebRequest req)
        {
            try
            {
                var t = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (string.IsNullOrEmpty(t)) return "";
                t = t.Trim();
                if (t.Length > 180) t = t.Substring(0, 180) + "...";
                return t;
            }
            catch { return ""; }
        }

        private static string GetProjectRoot()
        {
            var assets = Application.dataPath.Replace('\\', '/');
            if (assets.EndsWith("/Assets"))
                return assets.Substring(0, assets.Length - "/Assets".Length);
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string ToAbsolutePath(string projectRoot, string assetPath)
        {
            string rel = assetPath.Replace('\\', '/');
            if (rel.StartsWith("/")) rel = rel.Substring(1);
            return Path.Combine(projectRoot, rel);
        }

        private static byte[] GetTinyPngBytes()
        {
            return Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII="
            );
        }
    }
}
