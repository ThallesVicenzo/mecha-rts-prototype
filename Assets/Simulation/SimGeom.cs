using System;

namespace MechaRts.Sim
{
    public static class SimGeom
    {
        public static float Wrap(float radians)
        {
            const float pi2 = MathF.PI * 2f;
            radians %= pi2;
            if (radians > MathF.PI)
                radians -= pi2;
            if (radians < -MathF.PI)
                radians += pi2;
            return radians;
        }

        public static float DeltaAngle(float from, float to) => Wrap(to - from);

        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static bool CircleAabb(SimVec2 point, float radius, SimVec2 center, SimVec2 half)
        {
            float nx = Clamp(point.X, center.X - half.X, center.X + half.X);
            float ny = Clamp(point.Y, center.Y - half.Y, center.Y + half.Y);
            float dx = point.X - nx;
            float dy = point.Y - ny;
            return dx * dx + dy * dy < radius * radius;
        }

        public static bool SegmentHitsAabb(SimVec2 a, SimVec2 b, SimVec2 center, SimVec2 half)
        {
            float t0 = 0f;
            float t1 = 1f;
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            if (!Clip(-dx, a.X - (center.X - half.X), ref t0, ref t1)) return false;
            if (!Clip(dx, (center.X + half.X) - a.X, ref t0, ref t1)) return false;
            if (!Clip(-dy, a.Y - (center.Y - half.Y), ref t0, ref t1)) return false;
            if (!Clip(dy, (center.Y + half.Y) - a.Y, ref t0, ref t1)) return false;
            return true;
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (p < 0f)
            {
                float r = q / p;
                if (r > t1) return false;
                if (r > t0) t0 = r;
                return true;
            }

            if (p > 0f)
            {
                float r = q / p;
                if (r < t0) return false;
                if (r < t1) t1 = r;
                return true;
            }

            return q >= 0f;
        }
    }
}
