using System;
using System.Runtime.CompilerServices;

namespace Compass;

public static class Extensions
{
        
    public const float kEpsilon = 0.00001F;
    public const float kEpsilonNormalSqrt = 1e-15f;
        
        
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

       
        
        
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float min, float max)
    {
        if (value < min)
            value = min;
        else if (value > max)
            value = max;
        return value;
    }
}