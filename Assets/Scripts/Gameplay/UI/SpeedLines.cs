using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // Speed streaks rushing down the screen while Nitro is active. A runtime particle system sits just above
    // the top of the camera view and emits long thin stretched particles across its width.
    public class SpeedLines : MonoBehaviour
    {
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float emissionRate = 45f;
        [SerializeField] private float lineSpeed = 28f;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.35f);

        private ParticleSystem lines;
        private ParticleSystem.EmissionModule emission;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            lines = CreateLines();
            emission = lines.emission;
        }

        private void LateUpdate()
        {
            bool nitro = powerUpManager != null && powerUpManager.IsActive(CollectibleKind.Nitro);
            float rate = nitro ? emissionRate : 0f;
            if (!Mathf.Approximately(emission.rateOverTime.constant, rate)) emission.rateOverTime = rate;

            if (targetCamera != null)
            {
                float halfHeight = targetCamera.orthographicSize;
                float halfWidth = halfHeight * targetCamera.aspect;
                Vector3 cam = targetCamera.transform.position;
                lines.transform.position = new Vector3(cam.x, cam.y + halfHeight + 1f, 0f);
                var shape = lines.shape;
                shape.scale = new Vector3(halfWidth * 2f, 0.1f, 1f);
            }
        }

        private ParticleSystem CreateLines()
        {
            var go = new GameObject("NitroSpeedLines");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(lineSpeed * 0.8f, lineSpeed * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = lineColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(90f, 0f, 0f); // emit straight down the screen
            shape.scale = new Vector3(6f, 0.1f, 1f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 2f;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 4; // above road and cars, below the near-miss sparks

            ps.Play();
            return ps;
        }
    }
}
