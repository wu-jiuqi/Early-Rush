using System;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Ground-position follow only. No facing, animation or camera zoom input.</summary>
    [Serializable]
    public sealed class RushCameraV10
    {
        public Vector2 deadZone = new Vector2(2.6f, 1.8f);
        public float smoothing = .22f, groundFilter = .045f, maxSpeed = 18f, maxAcceleration = 85f;
        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        Vector2 anchor, filtered;

        public void Reset(Vector2 ground, Rect bounds, Vector2 viewHalf)
        {
            filtered = ground;
            Position = anchor = Clamp(ground, bounds, viewHalf);
            Velocity = Vector2.zero;
        }

        public Vector2 Step(Vector2 ground, Rect bounds, Vector2 viewHalf, float delta, bool playing)
        {
            // Pauses, loading and results freeze the exact image, including residual follow speed.
            if (!playing || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return Position;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(delta, .1f) * 120));
            float dt = Mathf.Min(delta, .1f) / steps;
            for (int i = 0; i < steps; i++)
            {
                filtered = Vector2.Lerp(filtered, ground, 1 - Mathf.Exp(-dt / Mathf.Max(.001f, groundFilter)));
                Vector2 offset = filtered - anchor;
                if (Mathf.Abs(offset.x) > deadZone.x) anchor.x = filtered.x - Mathf.Sign(offset.x) * deadZone.x;
                if (Mathf.Abs(offset.y) > deadZone.y) anchor.y = filtered.y - Mathf.Sign(offset.y) * deadZone.y;
                anchor = Clamp(anchor, bounds, viewHalf);
                Vector2 smoothVelocity = Velocity;
                Vector2 proposed = Vector2.SmoothDamp(Position, anchor, ref smoothVelocity, Mathf.Max(.01f, smoothing), Mathf.Max(.1f, maxSpeed), dt);
                Vector2 desired = (proposed - Position) / dt;
                Vector2 nextVelocity = Vector2.MoveTowards(Velocity, desired, Mathf.Max(.1f, maxAcceleration) * dt);
                nextVelocity = Vector2.ClampMagnitude(nextVelocity, Mathf.Max(.1f, maxSpeed));
                Vector2 next = Position + nextVelocity * dt;
                // Stop each axis at the target; never rebound after an abrupt stop or a map edge.
                if ((anchor.x - Position.x) * (anchor.x - next.x) <= 0) { next.x = anchor.x; nextVelocity.x = 0; }
                if ((anchor.y - Position.y) * (anchor.y - next.y) <= 0) { next.y = anchor.y; nextVelocity.y = 0; }
                Vector2 clamped = Clamp(next, bounds, viewHalf);
                if (clamped.x != next.x) nextVelocity.x = 0;
                if (clamped.y != next.y) nextVelocity.y = 0;
                Position = clamped; Velocity = nextVelocity;
            }
            return Position;
        }

        static Vector2 Clamp(Vector2 p, Rect bounds, Vector2 half)
        {
            p.x = bounds.width <= half.x * 2 ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + half.x, bounds.xMax - half.x);
            p.y = bounds.height <= half.y * 2 ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + half.y, bounds.yMax - half.y);
            return p;
        }

        // Pure numeric regression checks; run by the V10 editor validation entry point.
        public static int Validate()
        {
            var bounds = new Rect(-100, -100, 200, 200); var half = new Vector2(12, 7.5f);
            var camera = new RushCameraV10(); camera.Reset(Vector2.zero, bounds, half);
            for (int i = 0; i < 120; i++) camera.Step(new Vector2(Mathf.Sin(i) * .5f, 0), bounds, half, 1f / 60, true);
            if (camera.Position != Vector2.zero) throw new Exception("Camera moved for input inside dead zone");
            for (int i = 0; i < 120; i++) camera.Step(new Vector2(20, 20), bounds, half, 1f / 60, true);
            Vector2 before = camera.Position;
            camera.Step(new Vector2(80, 80), bounds, half, 30, false);
            if (camera.Position != before) throw new Exception("Camera drifted while paused");
            camera.Reset(new Vector2(99, 99), bounds, half);
            if (camera.Velocity != Vector2.zero || camera.Position.x > 88 || camera.Position.y > 92.5f) throw new Exception("Camera reset/boundary clamp failed");
            Vector2 baseline = Vector2.zero;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                camera.Reset(Vector2.zero, bounds, half);
                for (int frame = 1; frame <= fps * 5; frame++) camera.Step(new Vector2(frame * 5.3f / fps, 0), bounds, half, 1f / fps, true);
                if (fps == 30) baseline = camera.Position;
                else if (Vector2.Distance(baseline, camera.Position) > .15f) throw new Exception("Camera follow differs across frame rates");
            }
            return 6;
        }
    }
}
