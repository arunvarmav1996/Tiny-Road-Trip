using UnityEngine;
using UnityEngine.UI;

namespace TinyRoadTrip.Gameplay
{
    // Main menu volume button: tap to mute / unmute all game audio. The choice is saved and re-applied on
    // every launch. Uses AudioListener.volume, which is separate from AudioListener.pause (used by the pause menu).
    public class VolumeToggle : MonoBehaviour
    {
        public const string MutedKey = "TinyRoadTrip.Muted";

        [SerializeField] private Image icon;
        [Tooltip("Shown while muted (e.g. a slash across the speaker icon).")]
        [SerializeField] private GameObject mutedMark;
        [SerializeField, Range(0f, 1f)] private float mutedIconAlpha = 0.5f;

        public static bool IsMuted => PlayerPrefs.GetInt(MutedKey, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplySavedSetting()
        {
            AudioListener.volume = IsMuted ? 0f : 1f;
        }

        private void Start()
        {
            Refresh();
        }

        public void Toggle()
        {
            bool muted = !IsMuted;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = muted ? 0f : 1f;
            Refresh();
        }

        private void Refresh()
        {
            bool muted = IsMuted;
            if (icon != null)
            {
                Color c = icon.color;
                c.a = muted ? mutedIconAlpha : 1f;
                icon.color = c;
            }
            if (mutedMark != null) mutedMark.SetActive(muted);
        }
    }
}
