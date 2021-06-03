using System.Numerics;

namespace Compass
{
    public struct ImGuiCompassData
    {
        // NOTE (Chiv) This is the position of the player in the minimap coordinate system
        // It has positive down Y grow, we do calculations in a 'default' coordinate system
        // with positive up Y grow
        // => All game Y needs to be flipped.
        public const int NaviMapPlayerX = 72;
        public const int NaviMapPlayerY = -72;
        public const int NaviMapScaleOffset = 0x24C;
        public const int AreaMapScaleOffset = 0x374;
        public const uint WhiteColor = 0xFFFFFFFF;
        public const float ImGuiCompassHeight = 50f;
        public Vector2 PlayerPosition;
        public Vector2 ImGuiCompassCentre;
        public Vector2 ImGuiCompassBackgroundPMin;
        public Vector2 ImGuiCompassBackgroundPMax;
        public Vector2 ImGuiCompassBackgroundLinePMin;
        public Vector2 ImGuiCompassBackgroundLinePMax;
        public Vector2 ImGuiCompassDrawListPMin;
        public Vector2 ImGuiCompassDrawListPMax;
        public Vector2 ImGuiCompassDrawListBackgroundPMin;
        public Vector2 ImGuiCompassDrawListBackgroundPMax;
        public float ImGuiCompassHalfWidth;
        public float ImGuiCompassHalfHeight;
        public float ImGuiCompassScale;
        public float CompassHeightScale;
        public float DistanceScaleFactorForRotationIcons;
        public float HalfWidth40;
        public float HalfWidth28;
        public float ImGuiCompassUnit;
        public float RotationIconHalfWidth;
        public float MinScaleFactor;
        public float MaxDistance;
        public uint ImGuiBackgroundColourUInt32;
        public uint ImGuiBackgroundBorderColourUInt32;
        public uint ImGuiBackgroundLineColourUInt32;
        public int CurrentScaleOffset;
    }
}