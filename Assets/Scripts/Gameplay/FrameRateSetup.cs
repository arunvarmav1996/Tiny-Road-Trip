using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Mobile players default to 30 fps when targetFrameRate is left at -1, which makes a fast scrolling game look
    // choppy and input feel late. Ask for 60 before the first scene loads. vSyncCount must be 0 or the
    // quality level's vSync setting overrides targetFrameRate (mobile still presents in step with the display).
    public static class FrameRateSetup
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
