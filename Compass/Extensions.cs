using System;
using System.Numerics;
using ImGuiNET;

namespace Compass
{
    public static class Extensions
    {
        public const float kEpsilon = 0.00001F;
        public const float kEpsilonNormalSqrt = 1e-15f;

        public static Vector2 Rotate(this Vector2 v, float cos_a, float sin_a)
        {
            return new(v.X * cos_a - v.Y * sin_a, v.X * sin_a + v.Y * cos_a);
        }
        
        public static void Fill<T>(T[] array, T value)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
        }

        // Degrees-to-radians conversion constant (RO).
        public const float Deg2Rad = (float)Math.PI * 2F / 360F;

        // Radians-to-degrees conversion constant (RO).
        public const float Rad2Deg = 1F / Deg2Rad;

        public static float RoundUpToMultipleOf(float f, float multiple)
        {
            var remainder = Math.Abs(f) % multiple;
            if (remainder == 0)
                return f;

            if (f < 0)
                return -(Math.Abs(f) - remainder);
            return f + multiple - remainder;
        }
        
        public static void ImageRotated(IntPtr tex_id, Vector2 center, Vector2 size, float angle, Vector2 uv, Vector2 uv1)
        {
            
            var draw_list = ImGui.GetWindowDrawList();
            
            var cos_a = (float)Math.Cos(angle);
            var sin_a = (float)Math.Sin(angle);
            var pos = new[]
            {
                /*
                center + ImRotate(new Vector2(0, size.Y), cos_a, sin_a),
                center + ImRotate(new Vector2(+size.X, size.Y), cos_a, sin_a),
                center + ImRotate(new Vector2(+size.X, 0), cos_a, sin_a),
                center + ImRotate(new Vector2(0, 0), cos_a, sin_a)
                */
                center + Rotate(new Vector2(-size.X * 0.5f, -size.Y * 0.5f), cos_a, sin_a),
                center + Rotate(new Vector2(+size.X * 0.5f, -size.Y * 0.5f), cos_a, sin_a),
                center + Rotate(new Vector2(+size.X * 0.5f, +size.Y * 0.5f), cos_a, sin_a),
                center + Rotate(new Vector2(-size.X * 0.5f, +size.Y * 0.5f), cos_a, sin_a)
                
            };
            var uvs = new[] 
            { 
                new Vector2(uv.X, uv.Y),
                new Vector2(uv1.X, uv.Y),
                new Vector2(uv1.X, uv1.Y),
                new Vector2(uv.X, uv1.Y)
            };
            
            draw_list.AddImageQuad(tex_id, pos[0], pos[1], pos[2], pos[3], uvs[0], uvs[1], uvs[2], uvs[3]);
        }
        
        public static float Angle(Vector2 from, Vector2 to)
        {
            
            // sqrt(a) * sqrt(b) = sqrt(a * b) -- valid for real numbers
            var denominator = (float)Math.Sqrt(SqrMagnitude(from) * SqrMagnitude(to));
            if (denominator < kEpsilonNormalSqrt)
                return 0F;
            
            var dot = Clamp(Vector2.Dot(from, to) / denominator, -1F, 1F);
            return (float)Math.Acos(dot) * Rad2Deg;
        }
        
        public static float SignedAngle(Vector2 from, Vector2 to)
        {
            var unsigned_angle = Angle(from, to);
            var sign = Sign(from.X * to.Y - from.Y * to.X);
            return unsigned_angle * sign;
        }
        
        public static float Sign(float f) { return f >= 0F ? 1F : -1F; }
        public static float SqrMagnitude(this Vector2 vec)
        {
            return vec.X * vec.X + vec.Y * vec.Y;
        }
        
        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
                value = min;
            else if (value > max)
                value = max;
            return value;
        }
    }
}