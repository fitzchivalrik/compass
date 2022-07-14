using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using Dalamud.Configuration;
using Newtonsoft.Json;

namespace Compass;

public class Configuration : IPluginConfiguration {
    public bool   UseAreaMapAsSource;
    public bool   ShowOnlyCardinals;
    public bool   ShowCardinals = true;
    public bool   ShowInterCardinals;
    public bool   ShowWeatherIcon;
    public bool   ShowWeatherIconBorder  = true;
    public bool   ShowDistanceToTarget   = true;
    public float  AreaMapMaxDistance     = 360f;
    public string DistanceToTargetPrefix = string.Empty;
    public string DistanceToTargetSuffix = " yalm to target";

    public float                       ImGuiCompassScale            = 1f;
    public ImGuiCompassBackgroundStyle ImGuiCompassBackground       = ImGuiCompassBackgroundStyle.FilledAndBorder;
    public bool                        ImGuiCompassEnableBackground = true;
    public Vector4                     ImGuiBackgroundColour        = new(0.2f, 0.2f, 0.2f, 0.2f);
    public Vector4                     ImGuiBackgroundBorderColour  = new(0.4f, 0.4f, 0.4f, 1f);
    public Vector4                     ImGuiBackgroundLineColour    = new(0.4f, 0.4f, 0.4f, 1f);

    public float ImGuiCompassBackgroundRounding = 10f;
    public float ImGuiBackgroundBorderThickness = 1f;
    public float ImGuiBackgroundLineThickness   = 1f;
    public int   ImGuiCompassBackgroundLineOffset;

    public Vector2 ImGuiCompassPosition              = new(835, 515);
    public float   ImGuiCompassWidth                 = 250f;
    public float   ImGuiCompassReverseMaskPercentage = 1f;

    public bool    ImGuiCompassEnableCenterMarker = true;
    public bool    ImGuiCompassFlipCentreMarker;
    public int     ImGuiCompassCentreMarkerOffset = 20;
    public int     ImGuiCompassCardinalsOffset;
    public Vector2 ImGuiCompassWeatherIconOffset             = new(-20, -28);
    public float   ImGuiCompassWeatherIconScale              = 1f;
    public float   ImGuiCompassDistanceToTargetScale         = 1.75f;
    public float   ImGuiCompassMinimumIconScaleFactor        = 0.2f;
    public float   ImGuiCompassMinimumIconScaleFactorAreaMap = 0.0f;
    public Vector2 ImGuiCompassDistanceToTargetOffset        = new(-60, 20);
    public Vector4 ImGuiCompassDistanceToTargetColour        = new(1f, 1f, 1f, 1f);
    public bool    ImGuiCompassDistanceToTargetMouseOverPrio = true;
    public bool    ImGuiCompassEnable                        = true;

    public              bool[] ShouldHideOnUiObjectSerializer = System.Array.Empty<bool>();
    [JsonIgnore] public (string[] getUiObjectIdentifier, bool disable, string userFacingIdentifier)[] ShouldHideOnUiObject = null!;

    public HashSet<uint>     FilteredIconIds = new();
    public CompassVisibility Visibility      = CompassVisibility.Always;

    // ReSharper disable once UnassignedField.Global Leftover from Version 0
    public              bool HideInCombat;
    [JsonIgnore] public bool FreshInstall;

    public int Version { get; set; } = 1;
}

public class ConfigurationV0 : IPluginConfiguration {
    [JsonIgnore] public Vector2 AddonCompassOffset           = new(0, 0);
    [JsonIgnore] public float   AddonCompassScale            = 1f;
    [JsonIgnore] public int     AddonCompassWidth            = 550;
    [JsonIgnore] public int     AddonCompassBackgroundPartId = 1;
    [JsonIgnore] public bool    AddonCompassDisableBackground;
    [JsonIgnore] public bool    AddonCompassEnable;

    public bool   UseAreaMapAsSource;
    public bool   ShowOnlyCardinals;
    public bool   ShowWeatherIcon;
    public bool   ShowWeatherIconBorder  = true;
    public bool   ShowDistanceToTarget   = true;
    public float  AreaMapMaxDistance     = 360f;
    public string DistanceToTargetPrefix = string.Empty;
    public string DistanceToTargetSuffix = " yalm to target";

    public float                       ImGuiCompassScale            = 1f;
    public ImGuiCompassBackgroundStyle ImGuiCompassBackground       = ImGuiCompassBackgroundStyle.FilledAndBorder;
    public bool                        ImGuiCompassEnableBackground = true;
    public Vector4                     ImGuiBackgroundColour        = new(0.2f, 0.2f, 0.2f, 0.2f);
    public Vector4                     ImGuiBackgroundBorderColour  = new(0.4f, 0.4f, 0.4f, 1f);
    public Vector4                     ImGuiBackgroundLineColour    = new(0.4f, 0.4f, 0.4f, 1f);

    public float ImGuiCompassBackgroundRounding = 10f;
    public float ImGuiBackgroundBorderThickness = 1f;
    public float ImGuiBackgroundLineThickness   = 1f;
    public int   ImGuiCompassBackgroundLineOffset;

    public Vector2 ImGuiCompassPosition              = new(835, 515);
    public float   ImGuiCompassWidth                 = 250f;
    public float   ImGuiCompassReverseMaskPercentage = 1f;

    public bool    ImGuiCompassEnableCenterMarker = true;
    public bool    ImGuiCompassFlipCentreMarker;
    public int     ImGuiCompassCentreMarkerOffset = 20;
    public int     ImGuiCompassCardinalsOffset;
    public Vector2 ImGuiCompassWeatherIconOffset             = new(-20, -28);
    public float   ImGuiCompassWeatherIconScale              = 1f;
    public float   ImGuiCompassDistanceToTargetScale         = 1.75f;
    public float   ImGuiCompassMinimumIconScaleFactor        = 0.2f;
    public float   ImGuiCompassMinimumIconScaleFactorAreaMap = 0.0f;
    public Vector2 ImGuiCompassDistanceToTargetOffset        = new(-60, 20);
    public Vector4 ImGuiCompassDistanceToTargetColour        = new(1f, 1f, 1f, 1f);
    public bool    ImGuiCompassDistanceToTargetMouseOverPrio = true;
    public bool    ImGuiCompassEnable                        = true;

    public              bool[] ShouldHideOnUiObjectSerializer = System.Array.Empty<bool>();
    [JsonIgnore] public (string[] getUiObjectIdentifier, bool disable, string userFacingIdentifier)[] ShouldHideOnUiObject = null!;
    public              HashSet<uint> FilteredIconIds = new();
    public              bool HideInCombat;
    [JsonIgnore] public bool FreshInstall;
    public              bool HideInPvPMaps;
    public              bool HideInInstances;

    public int Version { get; set; } = 0;
}

public enum ImGuiCompassBackgroundStyle {
    Filled,
    Border,
    FilledAndBorder,
    Line
}

public enum CompassVisibility {
    Always,
    NotInCombat,
    InCombat
}