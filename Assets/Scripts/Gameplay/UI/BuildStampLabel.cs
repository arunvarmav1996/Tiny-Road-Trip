using UnityEngine;
using UnityEngine.UI;

namespace TinyRoadTrip.Gameplay
{
    // Small text in the bottom-left corner of every scene saying which build this is
    // (version, git branch, commit, build time), so a phone test can be matched to the code.
    // The text comes from BuildStamp.txt, written by the editor's BuildStamp step for each build.
    // Created at startup and kept across scenes; no scene setup needed.
    public static class BuildStampLabel
    {
        public const string ResourceName = "BuildStamp";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            TextAsset stamp = Resources.Load<TextAsset>(ResourceName);
            string text = stamp != null ? stamp.text.Trim() : "v" + Application.version + " · editor";

            var root = new GameObject("BuildStampLabel");
            Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above every game canvas
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var label = new GameObject("Text").AddComponent<Text>();
            label.transform.SetParent(root.transform, false);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.color = new Color(1f, 1f, 1f, 0.55f);
            label.alignment = TextAnchor.LowerLeft;
            label.raycastTarget = false; // never blocks taps or swipes
            label.text = text;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(16f, 8f);
            rect.sizeDelta = new Vector2(-32f, 40f);
        }
    }
}
