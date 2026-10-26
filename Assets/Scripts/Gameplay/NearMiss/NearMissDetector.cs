using System.Collections.Generic;
using TinyRoadTrip.Gameplay.Core;
using UnityEngine;

namespace TinyRoadTrip.Gameplay
{
    // A near miss = an obstacle's centre passes the player's centre line without a collision while the
    // horizontal gap between their colliders is within nearMissDistance. Each obstacle is evaluated once.
    // Uses the spawner's live obstacle list and collider bounds only (no physics queries).
    public class NearMissDetector : MonoBehaviour
    {
        [SerializeField] private Spawner obstacleSpawner;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private ComboManager comboManager;

        [Tooltip("Max horizontal gap (world units) between player and obstacle colliders that counts as a near miss. " +
                 "Cars settled in adjacent lanes are 0.36–0.48 apart, so the default only rewards last-moment dodges.")]
        [SerializeField, Min(0f)] private float nearMissDistance = 0.3f;

        // World position between the player and the passing car (for effects).
        public event System.Action<Vector3> NearMissAt;

        private void Update()
        {
            if (obstacleSpawner == null || playerCollider == null || comboManager == null) return;

            Bounds player = playerCollider.bounds;
            float lineY = player.center.y;
            IReadOnlyList<Spawner.ActiveObstacle> obstacles = obstacleSpawner.ActiveObstacles;

            for (int i = 0; i < obstacles.Count; i++)
            {
                Spawner.ActiveObstacle o = obstacles[i];
                if (o.NearMissResolved || o.Collider == null) continue;

                Bounds b = o.Collider.bounds;
                float y = b.center.y;
                if (o.PreviousY > lineY && y <= lineY)
                {
                    o.NearMissResolved = true;
                    if (!o.WasHit)
                    {
                        float gap = GameplayMath.HorizontalGap(player.min.x, player.max.x, b.min.x, b.max.x);
                        if (GameplayMath.IsNearMiss(gap, nearMissDistance))
                        {
                            comboManager.RegisterNearMiss();
                            float edgeX = b.center.x > player.center.x ? player.max.x : player.min.x;
                            NearMissAt?.Invoke(new Vector3(edgeX, lineY, player.center.z - 0.05f));
                        }
                    }
                }
                o.PreviousY = y;
            }
        }
    }
}
