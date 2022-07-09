﻿using System.Numerics;

namespace Compass;

// These variables are scaled based on user configuration and as such most cannot
// cannot be const. On configuration change, the vars are recomputed and then used
// during draw.
// It is a struct and not a class because that makes 'measurable' (draw time window of Dalamud)
// difference in draw time; a struct being faster.
public struct DrawData
{
    // NOTE (Chiv) This is the position of the player in the minimap coordinate system
    // It has positive down Y grow, we do calculations in a 'default' coordinate system
    // with positive up Y grow
    // => All game Y needs to be flipped.
    public const int NaviMapPlayerX = 72;
    public const int NaviMapPlayerY = -72;
    // As of 6.18
    public const int NaviMapScaleOffset = 0x24C;
    // As of 6.18
    public const int AreaMapScaleOffset = 0x374;
    public const uint WhiteColor = 0xFFFFFFFF;
    public const float Height = 50f;
    public Vector2 PlayerPosition;
    public Vector2 Centre;
    public Vector2 BackgroundPMin;
    public Vector2 BackgroundPMax;
    public Vector2 BackgroundLinePMin;
    public Vector2 BackgroundLinePMax;
    public Vector2 DrawListPMin;
    public Vector2 DrawListPMax;
    public Vector2 BackgroundDrawListPMin;
    public Vector2 BackgroundDrawListPMax;
    public Vector2 WeatherIconPMin;
    public Vector2 WeatherIconPMax;
    public Vector2 WeatherIconBorderPMin;
    public Vector2 WeatherIconBorderPMax;
    public Vector2 DistanceToTargetPMin;
    public float HalfWidth;
    public float HalfHeight;
    public float Scale;
    public float HeightScale;
    public float DistanceScaleFactorForRotationIcons;
    public float HalfWidth40;
    public float HalfWidth28;
    public float CompassUnit;
    public float RotationIconHalfWidth;
    public float MinScaleFactor;
    public float MaxDistance;
    public float DistanceToTargetScale;
    public uint BackgroundColourUInt32;
    public uint BackgroundBorderColourUInt32;
    public uint BackgroundLineColourUInt32;
    public uint DistanceToTargetColourUInt32;
    public int CurrentScaleOffset;
}