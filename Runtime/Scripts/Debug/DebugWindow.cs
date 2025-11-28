#if UNITY_WEBGL && UNITY_EDITOR
using System;
using System.Collections.Generic;
using Playgama.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Playgama.Debug
{
    public class DebugWindow : Singleton<DebugWindow>
    {
        private Canvas _canvas;
        private GameObject _overlay;
        private GameObject _window;
        private readonly Queue<Action> _windowQueue = new Queue<Action>();
        private bool _isWindowActive;

        public static void Initialize()
        {
            if (instance == null) return;
            instance.CreateCanvas();
        }

        private void CreateCanvas()
        {
            var canvasGO = new GameObject("DebugWindow");
            canvasGO.transform.SetParent(transform);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = Constants.SortOrder;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();
        }

        public static void ShowSimple(string title, Action onSuccess, Action onFail)
        {
            if (instance == null) return;

            instance.EnqueueWindow(() =>
            {
                instance.BuildWindow(title, new[]
                {
                    ("Success", onSuccess),
                    ("Fail", onFail)
                });
            });
        }

        public static void ShowYesNo(string title, Action<bool> callback)
        {
            if (instance == null) return;

            instance.EnqueueWindow(() =>
            {
                instance.BuildWindow(title, new[]
                {
                    ("Yes", (Action)(() => callback?.Invoke(true))),
                    ("No", (Action)(() => callback?.Invoke(false)))
                });
            });
        }

        public static void ShowInterstitial(Action<string> onStateChanged)
        {
            if (instance == null) return;

            instance.EnqueueWindow(() =>
            {
                instance.BuildAdWindow("Show Interstitial", false, onStateChanged);
            });
        }

        public static void ShowRewarded(Action<string> onStateChanged)
        {
            if (instance == null) return;

            instance.EnqueueWindow(() =>
            {
                instance.BuildAdWindow("Show Rewarded", true, onStateChanged);
            });
        }

        private void EnqueueWindow(Action showAction)
        {
            _windowQueue.Enqueue(showAction);
            if (!_isWindowActive)
            {
                ProcessQueue();
            }
        }

        private void ProcessQueue()
        {
            if (_windowQueue.Count == 0)
            {
                _isWindowActive = false;
                return;
            }

            _isWindowActive = true;
            var showAction = _windowQueue.Dequeue();
            showAction?.Invoke();
        }

        private void CloseWindow()
        {
            if (_overlay != null)
            {
                Destroy(_overlay);
                _overlay = null;
            }
            if (_window != null)
            {
                Destroy(_window);
                _window = null;
            }
            ProcessQueue();
        }

        private void BuildWindow(string title, (string label, Action onClick)[] buttons)
        {
            CreateOverlay();
            CreateWindowPanel();

            var titleText = CreateText(_window.transform, title, Constants.TitleFontSize, TextAnchor.MiddleCenter);
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -Constants.WindowPadding);
            titleRect.sizeDelta = new Vector2(0, 60);

            var buttonContainer = new GameObject("Buttons");
            buttonContainer.transform.SetParent(_window.transform, false);
            var containerRect = buttonContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.pivot = new Vector2(0.5f, 0);
            containerRect.anchoredPosition = new Vector2(0, Constants.WindowPadding);
            containerRect.sizeDelta = new Vector2(0, Constants.ButtonHeight);

            var layout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = Constants.ButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(
                (int)Constants.WindowPadding,
                (int)Constants.WindowPadding,
                0,
                0
            );

            foreach (var (label, onClick) in buttons)
            {
                CreateButton(buttonContainer.transform, label, () =>
                {
                    CloseWindow();
                    onClick?.Invoke();
                });
            }

            float windowHeight = Constants.WindowPadding * 2 + 60 + Constants.ButtonSpacing + Constants.ButtonHeight;
            var windowRect = _window.GetComponent<RectTransform>();
            windowRect.sizeDelta = new Vector2(Constants.WindowWidth, windowHeight);
        }

        private void BuildAdWindow(string title, bool isRewarded, Action<string> onStateChanged)
        {
            CreateOverlay();
            CreateWindowPanel();

            var titleText = CreateText(_window.transform, title, Constants.TitleFontSize, TextAnchor.MiddleCenter);
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -Constants.WindowPadding);
            titleRect.sizeDelta = new Vector2(0, 60);

            var stateText = CreateText(_window.transform, "State: Opened", Constants.StateFontSize, TextAnchor.MiddleCenter);
            var stateRect = stateText.GetComponent<RectTransform>();
            stateRect.anchorMin = new Vector2(0, 1);
            stateRect.anchorMax = new Vector2(1, 1);
            stateRect.pivot = new Vector2(0.5f, 1);
            stateRect.anchoredPosition = new Vector2(0, -Constants.WindowPadding - 65);
            stateRect.sizeDelta = new Vector2(0, 40);

            var buttonContainer = new GameObject("Buttons");
            buttonContainer.transform.SetParent(_window.transform, false);
            var containerRect = buttonContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.pivot = new Vector2(0.5f, 0);
            containerRect.anchoredPosition = new Vector2(0, Constants.WindowPadding);
            containerRect.sizeDelta = new Vector2(0, Constants.ButtonHeight);

            var layout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = Constants.ButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(
                (int)Constants.WindowPadding,
                (int)Constants.WindowPadding,
                0,
                0
            );

            if (isRewarded)
            {
                CreateButton(buttonContainer.transform, "Reward", () =>
                {
                    stateText.text = "State: Rewarded";
                    onStateChanged?.Invoke("Rewarded");
                });
            }

            CreateButton(buttonContainer.transform, "Close", () =>
            {
                CloseWindow();
                onStateChanged?.Invoke("Closed");
            });

            CreateButton(buttonContainer.transform, "Fail", () =>
            {
                CloseWindow();
                onStateChanged?.Invoke("Failed");
            });

            float windowHeight = Constants.WindowPadding * 2 + 60 + 40 + Constants.ButtonSpacing + Constants.ButtonHeight;
            var windowRect = _window.GetComponent<RectTransform>();
            windowRect.sizeDelta = new Vector2(Constants.WindowWidth, windowHeight);
        }

        private void CreateOverlay()
        {
            _overlay = new GameObject("Overlay");
            _overlay.transform.SetParent(_canvas.transform, false);

            var overlayRect = _overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            var overlayImage = _overlay.AddComponent<Image>();
            overlayImage.color = Constants.OverlayColor;
            overlayImage.raycastTarget = true;
        }

        private void CreateWindowPanel()
        {
            _window = new GameObject("Window");
            _window.transform.SetParent(_canvas.transform, false);

            var windowRect = _window.AddComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(Constants.WindowWidth, 200);

            var windowImage = _window.AddComponent<Image>();
            windowImage.color = Constants.WindowColor;
        }

        private Text CreateText(Transform parent, string content, float fontSize, TextAnchor alignment)
        {
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(parent, false);

            var text = textGO.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = (int)fontSize;
            text.alignment = alignment;
            text.color = Constants.TextColor;

            return text;
        }

        private void CreateButton(Transform parent, string label, Action onClick)
        {
            var buttonGO = new GameObject(label);
            buttonGO.transform.SetParent(parent, false);

            buttonGO.AddComponent<RectTransform>();

            var buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = Constants.ButtonColor;

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => onClick?.Invoke());

            var buttonText = CreateText(buttonGO.transform, label, 20, TextAnchor.MiddleCenter);
            var textRect = buttonText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }

        private static class Constants
        {
            // Canvas
            public const int SortOrder = 32767;

            // Window
            public const float WindowWidth = 700f;
            public const float WindowHeight = 200f;
            public const float WindowPadding = 40f;
            public const float ButtonHeight = 50f;
            public const float ButtonSpacing = 20f;
            public const float TitleFontSize = 32f;
            public const float StateFontSize = 24f;

            // Colors (grayscale)
            public static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.75f);
            public static readonly Color WindowColor = new Color(0.12f, 0.12f, 0.12f, 0.98f);
            public static readonly Color ButtonColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            public static readonly Color TextColor = Color.white;
        }
    }
}
#endif
