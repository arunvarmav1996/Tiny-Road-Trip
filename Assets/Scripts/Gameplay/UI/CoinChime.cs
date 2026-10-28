using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Coin pickup "ding". The clip is synthesised at startup (two bell-like partials with a fast decay), so no
    // audio asset is needed. Pitch climbs with quick coin chains and with the near-miss combo, so streaks sound
    // rewarding. Respects pause (AudioListener.pause) and mute (AudioListener.volume) like every other source.
    public class CoinChime : MonoBehaviour
    {
        [SerializeField] private CollectibleSpawner collectibleSpawner;
        [SerializeField] private ComboManager comboManager;

        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField] private float baseFrequency = 1320f; // E6
        [Tooltip("Coins collected within this many seconds of each other count as one chain.")]
        [SerializeField] private float chainWindow = 0.7f;
        [SerializeField] private float pitchStepPerCoin = 0.05f;
        [SerializeField] private int maxChainSteps = 8;
        [SerializeField] private float pitchStepPerCombo = 0.04f;
        [SerializeField] private int maxComboSteps = 6;

        private AudioSource source;
        private int chain;
        private float lastCoinTime = -10f;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = CreateClip(baseFrequency);
        }

        private void OnEnable()
        {
            if (collectibleSpawner != null) collectibleSpawner.Collected += OnCollected;
        }

        private void OnDisable()
        {
            if (collectibleSpawner != null) collectibleSpawner.Collected -= OnCollected;
        }

        private void OnCollected(CollectibleDefinition definition, Vector3 position)
        {
            if (definition.kind != CollectibleKind.Coin) return;

            chain = Time.time - lastCoinTime <= chainWindow ? chain + 1 : 0;
            lastCoinTime = Time.time;

            int combo = comboManager != null ? comboManager.Combo : 0;
            source.pitch = 1f + pitchStepPerCoin * Mathf.Min(chain, maxChainSteps)
                              + pitchStepPerCombo * Mathf.Min(combo, maxComboSteps);
            source.PlayOneShot(source.clip, volume);
        }

        private static AudioClip CreateClip(float frequency)
        {
            const int sampleRate = 44100;
            const float length = 0.28f;
            int samples = Mathf.CeilToInt(sampleRate * length);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float attack = Mathf.Clamp01(t / 0.004f);
                float body = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 14f);
                float shimmer = 0.45f * Mathf.Sin(2f * Mathf.PI * frequency * 2.01f * t) * Mathf.Exp(-t * 26f);
                data[i] = 0.8f * attack * (body + shimmer);
            }
            AudioClip clip = AudioClip.Create("CoinChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
