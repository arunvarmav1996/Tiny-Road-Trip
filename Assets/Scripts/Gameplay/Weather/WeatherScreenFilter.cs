using System;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Colour grade for one weather: how the whole game view is filtered while it is active.
    [Serializable]
    public struct ScreenGrade
    {
        [Tooltip("Colour the screen is multiplied towards.")]
        public Color tint;
        [Range(0f, 1f)] public float tintStrength;
        [Tooltip("1 = unchanged, above 1 = more vivid, below 1 = washed out.")]
        [Range(0f, 2f)] public float saturation;
        [Range(0.5f, 1.5f)] public float contrast;
        [Range(0.5f, 1.5f)] public float brightness;

        public static ScreenGrade Neutral => new ScreenGrade
        {
            tint = Color.white, tintStrength = 0f, saturation = 1f, contrast = 1f, brightness = 1f,
        };

        public static ScreenGrade Lerp(ScreenGrade a, ScreenGrade b, float t) => new ScreenGrade
        {
            tint = Color.Lerp(a.tint, b.tint, t),
            tintStrength = Mathf.Lerp(a.tintStrength, b.tintStrength, t),
            saturation = Mathf.Lerp(a.saturation, b.saturation, t),
            contrast = Mathf.Lerp(a.contrast, b.contrast, t),
            brightness = Mathf.Lerp(a.brightness, b.brightness, t),
        };

        public bool IsNeutral =>
            tintStrength < 0.001f && Mathf.Abs(saturation - 1f) < 0.001f &&
            Mathf.Abs(contrast - 1f) < 0.001f && Mathf.Abs(brightness - 1f) < 0.001f;
    }

    // Applies a ScreenGrade to everything the camera draws (screen-space UI is drawn afterwards, so the HUD
    // stays untouched). Turns itself off while the grade is neutral, so clear weather costs nothing.
    [RequireComponent(typeof(Camera))]
    public class WeatherScreenFilter : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int GradeId = Shader.PropertyToID("_Grade");

        private Material material;

        public void Init(Shader shader)
        {
            if (shader != null && shader.isSupported)
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            enabled = false;
        }

        public void Set(ScreenGrade grade)
        {
            bool active = material != null && !grade.IsNeutral;
            if (enabled != active) enabled = active;
            if (!active) return;
            material.SetColor(TintId, grade.tint);
            material.SetVector(GradeId, new Vector4(grade.brightness, grade.contrast, grade.saturation, grade.tintStrength));
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null) Graphics.Blit(source, destination);
            else Graphics.Blit(source, destination, material);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
