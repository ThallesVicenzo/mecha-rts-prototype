using System;

namespace MechaRts.Sim
{
    public struct SimVec2
    {
        public float X;
        public float Y;

        public SimVec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static SimVec2 Zero => new SimVec2(0f, 0f);

        public float SqrMagnitude => X * X + Y * Y;

        public float Magnitude => MathF.Sqrt(X * X + Y * Y);

        public float Angle => MathF.Atan2(Y, X);

        public static SimVec2 operator +(SimVec2 a, SimVec2 b) => new SimVec2(a.X + b.X, a.Y + b.Y);

        public static SimVec2 operator -(SimVec2 a, SimVec2 b) => new SimVec2(a.X - b.X, a.Y - b.Y);

        public static SimVec2 operator *(SimVec2 a, float s) => new SimVec2(a.X * s, a.Y * s);

        public static SimVec2 operator -(SimVec2 a) => new SimVec2(-a.X, -a.Y);

        public static float DistanceSq(SimVec2 a, SimVec2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        public static SimVec2 FromAngle(float radians) => new SimVec2(MathF.Cos(radians), MathF.Sin(radians));

        public static SimVec2 Normalize(SimVec2 v)
        {
            float m = v.Magnitude;
            if (m < 1e-5f)
                return Zero;
            return new SimVec2(v.X / m, v.Y / m);
        }

        public static SimVec2 MoveTowards(SimVec2 from, SimVec2 to, float maxDelta)
        {
            SimVec2 d = to - from;
            float m = d.Magnitude;
            if (m <= maxDelta || m < 1e-6f)
                return to;
            return from + d * (maxDelta / m);
        }
    }
}
