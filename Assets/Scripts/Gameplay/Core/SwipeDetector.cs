namespace TinyRoadTrip.Gameplay.Core
{
    public enum SwipeDirection
    {
        None,
        Left,
        Right,
        Up,
        Down,
    }

    // One swipe per touch. Fires as soon as the drag passes the threshold (no need to lift the finger);
    // if the finger lifts before any move frame reported the distance, the release is checked instead.
    public sealed class SwipeDetector
    {
        private readonly float minDistanceSqr;
        private float startX;
        private float startY;
        private bool tracking;
        private bool fired;

        public SwipeDetector(float minDistance)
        {
            minDistanceSqr = minDistance * minDistance;
        }

        public bool IsTracking => tracking;

        public void Begin(float x, float y)
        {
            startX = x;
            startY = y;
            tracking = true;
            fired = false;
        }

        // Call while the finger is down (Moved / Stationary).
        public SwipeDirection Move(float x, float y)
        {
            if (!tracking || fired) return SwipeDirection.None;
            float dx = x - startX;
            float dy = y - startY;
            if (dx * dx + dy * dy < minDistanceSqr) return SwipeDirection.None;
            fired = true;
            return Direction(dx, dy);
        }

        // Call when the finger lifts or the touch is cancelled.
        public SwipeDirection End(float x, float y)
        {
            SwipeDirection result = Move(x, y);
            tracking = false;
            return result;
        }

        // Ignore the rest of the current touch (e.g. the tap that pressed Resume).
        public void Cancel()
        {
            tracking = false;
            fired = true;
        }

        private static SwipeDirection Direction(float dx, float dy)
        {
            if ((dx < 0 ? -dx : dx) > (dy < 0 ? -dy : dy))
                return dx < 0f ? SwipeDirection.Left : SwipeDirection.Right;
            return dy > 0f ? SwipeDirection.Up : SwipeDirection.Down;
        }
    }
}
