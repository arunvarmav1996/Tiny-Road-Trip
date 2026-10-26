using UnityEngine;
using UnityEngine.UI;

namespace TinyRoadTrip.Gameplay
{
    // Hit feedback ("juice"): camera shake + white flash on crash and on a shield save, sparks + a small kick on
    // near misses. Shake and flash use unscaled time so they still play while game over freezes the game.
    // The spark particles and flash overlay are created at runtime; only the references below are wired.
    public class GameFeel : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private NearMissDetector nearMissDetector;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private Camera targetCamera;

        [Header("Crash")]
        [SerializeField] private float crashShakeAmount = 0.28f;
        [SerializeField] private float crashShakeDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float crashFlashAlpha = 0.6f;
        [SerializeField] private float flashFadeDuration = 0.35f;

        [Header("Shield save")]
        [SerializeField] private float shieldShakeAmount = 0.15f;
        [SerializeField] private float shieldShakeDuration = 0.2f;
        [SerializeField, Range(0f, 1f)] private float shieldFlashAlpha = 0.3f;

        [Header("Near miss")]
        [SerializeField] private float nearMissShakeAmount = 0.06f;
        [SerializeField] private float nearMissShakeDuration = 0.12f;
        [SerializeField, Min(1)] private int sparkCount = 14;
        [SerializeField] private Color sparkColor = new Color(1f, 0.85f, 0.3f);

        private Transform cameraTransform;
        private Vector3 cameraBasePosition;
        private float shakeAmount;
        private float shakeDuration;
        private float shakeRemaining;

        private Image flash;
        private float flashStartAlpha;
        private float flashRemaining;

        private ParticleSystem sparks;

        // Subscriptions in Awake/OnDestroy because "enabled" is toggled to tick only while animating.
        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                cameraTransform = targetCamera.transform;
                cameraBasePosition = cameraTransform.localPosition;
            }

            if (gameManager != null) gameManager.GameOver += OnCrash;
            if (nearMissDetector != null) nearMissDetector.NearMissAt += OnNearMiss;

            flash = CreateFlashOverlay();
            sparks = CreateSparks();
            enabled = false;
        }

        private void Start()
        {
            // PowerUpManager builds its Shield reference in Awake.
            if (powerUpManager != null && powerUpManager.Shield != null) powerUpManager.Shield.Absorbed += OnShieldSave;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.GameOver -= OnCrash;
            if (nearMissDetector != null) nearMissDetector.NearMissAt -= OnNearMiss;
            if (powerUpManager != null && powerUpManager.Shield != null) powerUpManager.Shield.Absorbed -= OnShieldSave;
        }

        private void OnCrash()
        {
            Shake(crashShakeAmount, crashShakeDuration);
            Flash(crashFlashAlpha);
        }

        private void OnShieldSave()
        {
            Shake(shieldShakeAmount, shieldShakeDuration);
            Flash(shieldFlashAlpha);
        }

        private void OnNearMiss(Vector3 position)
        {
            Shake(nearMissShakeAmount, nearMissShakeDuration);
            if (sparks != null)
            {
                sparks.transform.position = position;
                sparks.Emit(sparkCount);
            }
        }

        public void Shake(float amount, float duration)
        {
            if (cameraTransform == null || duration <= 0f) return;
            // A stronger shake replaces a weaker one; a weaker one never cuts a strong one short.
            if (shakeRemaining > 0f && amount < shakeAmount * (shakeRemaining / shakeDuration)) return;
            shakeAmount = amount;
            shakeDuration = duration;
            shakeRemaining = duration;
            enabled = true;
        }

        public void Flash(float alpha)
        {
            if (flash == null || alpha <= 0f) return;
            flashStartAlpha = alpha;
            flashRemaining = flashFadeDuration;
            flash.enabled = true;
            enabled = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool busy = false;

            if (shakeRemaining > 0f && cameraTransform != null)
            {
                shakeRemaining -= dt;
                if (shakeRemaining > 0f)
                {
                    float strength = shakeAmount * (shakeRemaining / shakeDuration);
                    Vector2 offset = Random.insideUnitCircle * strength;
                    cameraTransform.localPosition = cameraBasePosition + new Vector3(offset.x, offset.y, 0f);
                    busy = true;
                }
                else
                {
                    cameraTransform.localPosition = cameraBasePosition;
                }
            }

            if (flashRemaining > 0f && flash != null)
            {
                flashRemaining -= dt;
                float a = flashFadeDuration > 0f ? flashStartAlpha * Mathf.Clamp01(flashRemaining / flashFadeDuration) : 0f;
                flash.color = new Color(1f, 1f, 1f, a);
                if (flashRemaining > 0f) busy = true;
                else flash.enabled = false;
            }

            if (!busy) enabled = false;
        }

        private void OnDisable()
        {
            if (cameraTransform != null && shakeRemaining <= 0f) cameraTransform.localPosition = cameraBasePosition;
        }

        // Full-screen white overlay above the game world, below the Game Over / pause panels.
        private Image CreateFlashOverlay()
        {
            var go = new GameObject("HitFlash", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -2;
            var imageGo = new GameObject("Flash", typeof(RectTransform));
            var rt = (RectTransform)imageGo.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = imageGo.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private ParticleSystem CreateSparks()
        {
            var go = new GameObject("NearMissSparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = sparkColor;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = false; // bursts come from Emit()

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(sparkColor, 0.4f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 5; // above road, cars and player
            return ps;
        }
    }
}
