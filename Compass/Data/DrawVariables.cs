using System;
using System.Numerics;
using ImGuiNET;

namespace Compass.Data;

// These variables are scaled based on user configuration and as such most cannot
// cannot be const. On configuration change, the vars are recomputed and then used
// during draw.
// It is a struct and not a class because that makes 'measurable' (draw time window of Dalamud)
// difference in draw time; a struct being faster.
// TODO: Split up in smaller structs
internal struct DrawVariables
{
    internal readonly Vector2 Centre                 = new(835 + 125f, 515 + 25f);
    internal readonly Vector2 BackgroundPMin         = Vector2.Zero;
    internal readonly Vector2 BackgroundPMax         = Vector2.Zero;
    internal readonly Vector2 BackgroundLinePMin     = Vector2.Zero;
    internal readonly Vector2 BackgroundLinePMax     = Vector2.Zero;
    internal readonly Vector2 DrawListPMin           = Vector2.Zero;
    internal readonly Vector2 DrawListPMax           = Vector2.Zero;
    internal readonly Vector2 BackgroundDrawListPMin = Vector2.Zero;
    internal readonly Vector2 BackgroundDrawListPMax = Vector2.Zero;
    internal readonly Vector2 WeatherIconPMin        = Vector2.Zero;
    internal readonly Vector2 WeatherIconPMax        = Vector2.Zero;
    internal readonly Vector2 WeatherIconBorderPMin  = Vector2.Zero;
    internal readonly Vector2 WeatherIconBorderPMax  = Vector2.Zero;
    internal readonly Vector2 DistanceToTargetPMin   = Vector2.Zero;

    internal readonly float HalfWidth                           = 125f;
    internal readonly float HalfHeight                          = 125f;
    internal readonly float Scale                               = 1f;
    internal readonly float HeightScale                         = 1f;
    internal readonly float DistanceScaleFactorForRotationIcons = 1f;
    internal readonly float HalfWidth40                         = 20f;
    internal readonly float HalfWidth28                         = 14f;
    internal readonly float CompassUnit                         = 0f;
    internal readonly float RotationIconHalfWidth               = 12f;
    internal readonly float MinScaleFactor                      = 0.2f;
    internal readonly float MaxDistance                         = 180f;
    internal readonly float DistanceToTargetScale               = 1f;
    internal readonly uint  BackgroundColourUInt32              = Constant.WhiteColour;
    internal readonly uint  BackgroundBorderColourUInt32        = Constant.WhiteColour;
    internal readonly uint  BackgroundLineColourUInt32          = Constant.WhiteColour;
    internal readonly uint  DistanceToTargetColourUInt32        = Constant.WhiteColour;
    internal readonly int   CurrentScaleOffset                  = Constant.NaviMapScaleOffset;
    internal readonly int   ComponentIconLoopStart              = 0;
    internal readonly int   ComponentIconLoopEnd                = 0;

    public DrawVariables()
    { }

    internal DrawVariables(Configuration config)
    {
        Scale                               = config.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
        HeightScale                         = ImGui.GetIO().FontGlobalScale;
        DistanceScaleFactorForRotationIcons = Scale * 0.7f;

        RotationIconHalfWidth = 16f * DistanceScaleFactorForRotationIcons;
        HalfWidth             = config.ImGuiCompassWidth * 0.5f;
        HalfHeight            = Constant.CompassHeight * 0.5f * HeightScale;

        CompassUnit = config.ImGuiCompassWidth / (2f * MathF.PI);
        Centre =
            new Vector2(config.ImGuiCompassPosition.X + HalfWidth,
                config.ImGuiCompassPosition.Y + HalfHeight);

        BackgroundPMin = new Vector2(
            Centre.X - 5 - HalfWidth * config.ImGuiCompassReverseMaskPercentage
          , Centre.Y - HalfHeight * 0.5f - 2
        );
        BackgroundPMax = new Vector2(
            Centre.X + 5 + HalfWidth * config.ImGuiCompassReverseMaskPercentage
          , Centre.Y + HalfHeight * 0.5f + 2
        );
        BackgroundLinePMin = BackgroundPMin with
        {
            Y = config.ImGuiCompassBackgroundLineOffset + BackgroundPMin.Y + HalfHeight
        };
        BackgroundLinePMax = BackgroundPMax with
        {
            Y = config.ImGuiCompassBackgroundLineOffset + BackgroundPMin.Y + HalfHeight
        };
        DistanceToTargetPMin = Centre + config.ImGuiCompassDistanceToTargetOffset;
        DistanceToTargetScale =
            ImGui.GetIO().FontGlobalScale * config.ImGuiCompassDistanceToTargetScale;
        DrawListPMin =
            BackgroundPMin + new Vector2(-2, -100);
        DrawListPMax =
            BackgroundPMax + new Vector2(2, 100);
        BackgroundDrawListPMin =
            BackgroundPMin + new Vector2(-3, -100);
        BackgroundDrawListPMax =
            BackgroundPMax + new Vector2(3, 100);

        WeatherIconPMin =
            BackgroundPMin + config.ImGuiCompassWeatherIconOffset + new Vector2(2, 1) * config.ImGuiCompassWeatherIconScale;
        WeatherIconPMax =
            WeatherIconPMin + new Vector2(32, 32) * config.ImGuiCompassWeatherIconScale;

        WeatherIconBorderPMin = config.ShowWeatherIconBorder
            ? BackgroundPMin + config.ImGuiCompassWeatherIconOffset
            : Vector2.Zero;
        WeatherIconBorderPMax = config.ShowWeatherIconBorder
            ? WeatherIconBorderPMin + new Vector2(36, 36) * config.ImGuiCompassWeatherIconScale
            : Vector2.Zero;

        HalfWidth40 = 20 * Scale;
        HalfWidth28 = 14 * Scale;

        BackgroundColourUInt32       = ImGui.ColorConvertFloat4ToU32(config.ImGuiBackgroundColour);
        BackgroundBorderColourUInt32 = ImGui.ColorConvertFloat4ToU32(config.ImGuiBackgroundBorderColour);
        BackgroundLineColourUInt32   = ImGui.ColorConvertFloat4ToU32(config.ImGuiBackgroundLineColour);
        DistanceToTargetColourUInt32 = ImGui.ColorConvertFloat4ToU32(config.ImGuiCompassDistanceToTargetColour);

        MinScaleFactor = config.UseAreaMapAsSource ? config.ImGuiCompassMinimumIconScaleFactorAreaMap : config.ImGuiCompassMinimumIconScaleFactor;
        MaxDistance    = config.UseAreaMapAsSource ? config.AreaMapMaxDistance : 180f;
        CurrentScaleOffset = config.UseAreaMapAsSource
            ? Constant.AreaMapScaleOffset
            : Constant.NaviMapScaleOffset;

        ComponentIconLoopStart = config.UseAreaMapAsSource ? 7 : 4;
        ComponentIconLoopEnd   = config.UseAreaMapAsSource ? 7 : 6;
    }
}