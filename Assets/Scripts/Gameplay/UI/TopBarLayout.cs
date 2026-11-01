using UnityEngine;
using UnityEngine.UI;

namespace TinyRoadTrip.Gameplay
{
    // Editing helper for the gameplay HUD's top bar. Change the numbers in the inspector and the bar, its
    // Left / Center / Right slots and the area below it are laid out again (edit mode only; the result is
    // saved in the scene like any manual RectTransform edit). Anything placed inside a slot is free to move.
    [RequireComponent(typeof(RectTransform))]
    public class TopBarLayout : MonoBehaviour
    {
        [Header("Bar (canvas units, reference 1080 x 1920)")]
        [SerializeField, Min(40f)] private float barHeight = 170f;
        [Tooltip("Gap between the top of the safe area and the bar.")]
        [SerializeField, Min(0f)] private float topMargin = 0f;
        [Tooltip("Gap between the screen edges and the Left / Right slots.")]
        [SerializeField, Min(0f)] private float sidePadding = 24f;
        [SerializeField, Range(0f, 1f)] private float backgroundAlpha = 0.35f;

        [Header("Slots")]
        [SerializeField] private RectTransform left;
        [SerializeField, Min(0f)] private float leftWidth = 320f;
        [SerializeField] private RectTransform center;
        [SerializeField, Min(0f)] private float centerWidth = 420f;
        [SerializeField] private RectTransform right;
        [SerializeField, Min(0f)] private float rightWidth = 150f;

        [Header("Below the bar")]
        [Tooltip("Full-width area under the bar (power-up timers).")]
        [SerializeField] private RectTransform belowBar;
        [SerializeField, Min(0f)] private float belowBarGap = 16f;
        [SerializeField, Min(0f)] private float belowBarHeight = 420f;

        [SerializeField] private Image background;

        [ContextMenu("Apply Layout")]
        public void Apply()
        {
            var bar = (RectTransform)transform;
            SetRect(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -topMargin), new Vector2(0f, barHeight));

            if (left != null)
                SetRect(left, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                        new Vector2(sidePadding, 0f), new Vector2(leftWidth, 0f));
            if (center != null)
                SetRect(center, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(centerWidth, 0f));
            if (right != null)
                SetRect(right, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                        new Vector2(-sidePadding, 0f), new Vector2(rightWidth, 0f));

            if (belowBar != null)
                SetRect(belowBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -(topMargin + barHeight + belowBarGap)), new Vector2(-2f * sidePadding, belowBarHeight));

            if (background != null)
            {
                Color c = background.color;
                c.a = backgroundAlpha;
                background.color = c;
                background.enabled = backgroundAlpha > 0f;
            }
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                    Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // RectTransform changes are not allowed inside OnValidate itself.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying) return;
                UnityEditor.Undo.RecordObjects(Targets(), "Top Bar Layout");
                Apply();
            };
        }

        private Object[] Targets()
        {
            var list = new System.Collections.Generic.List<Object> { transform };
            if (left != null) list.Add(left);
            if (center != null) list.Add(center);
            if (right != null) list.Add(right);
            if (belowBar != null) list.Add(belowBar);
            if (background != null) list.Add(background);
            return list.ToArray();
        }
#endif
    }
}
