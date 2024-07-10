using System;
using System.Linq;
using System.Numerics;
using Compass.Data;
using Compass.UI;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Compass;

internal class Compass
{
    private readonly ICondition     _condition;
    private readonly IGameGui       _gameGui;
    private readonly ITargetManager _targetManager;
    private readonly Configuration  _config;
    private          int            _currentUiObjectIndex;
    private          bool           _dirty;

    private DrawVariables _drawVariables;

    private Pointers _pointers;

    // NOTE: This is the position of the player in the minimap coordinate system.
    // The position gets updated on each CompassWindow.Draw with the position of the 
    // player marker in the map.
    // The minimap coordinate system has positive down Y grow;
    // we do calculations in a 'default' coordinate system with positive up Y grow.
    // => All game Y needs to be flipped.
    private Vector2  _playerPosition = new(Constant.NaviMapPlayerX, Constant.NaviMapPlayerY);
    private bool     _shouldHideCompass;
    private bool     _shouldHideCompassIteration;
    private string[] _uiIdentifiers = Array.Empty<string>();


    internal Compass(
        ICondition                        condition
      , ITargetManager                    targetManager
      , IGameGui gameGui
      , Configuration                     config
    )
    {
        _condition     = condition;
        _gameGui       = gameGui;
        _targetManager = targetManager;

        _config = config;
        UpdateCachedVariables();
    }

    internal void Draw()
    {
        if (!_config.ImGuiCompassEnable) return;
        // To prevent Aesthetician crash. This is unfortunate, but I do not know 
        // of an event or similar which gets fired when transitioning to Aesthetician.
        // Therefore, we need to check every frame.
        if (_gameGui.GetAddonByName("_CharaMakeBgSelector") != nint.Zero)
        {
            _dirty = true;
            return;
        }

        if (_dirty)
        {
            _dirty = !UpdateCachedVariables();
            return;
        }

        switch (_config.Visibility)
        {
            case CompassVisibility.Always:
                break;
            case CompassVisibility.NotInCombat:
                if (_condition[ConditionFlag.InCombat]) return;
                break;
            case CompassVisibility.InCombat:
                if (!_condition[ConditionFlag.InCombat]) return;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        UpdateHideCompass();
        if (_shouldHideCompass) return;
        float cameraRotationInRadian;
        try
        {
            unsafe
            {
                var atkBase = _pointers.CurrentSourceBase;
                if (atkBase->UldManager.LoadedState != AtkLoadState.Loaded) return;
                if (!atkBase->IsVisible) return;
                // 0 == Facing North, -PI/2 facing east, PI/2 facing west.
                //var _miniMapIconsRootComponentNode = (AtkComponentNode*)_naviMap->ULDData.NodeList[2];
                // Minimap rotation thingy is even already flipped!
                // And apparently even accessible & updated if _NaviMap is disabled
                // => This leads to jerky behaviour though
                cameraRotationInRadian = *_pointers.PlayerViewTriangleRotation * Util.Deg2Rad;
            }
        }
        catch
        {
            // ignored
            return;
        }

        _playerPosition = CompassWindow.Draw(
            _drawVariables,
            _pointers,
            cameraRotationInRadian,
            _playerPosition,
            _config
        );
    }

    internal bool UpdateCachedVariables()
    {
        if (!UpdateMapPointersCache()) return false;
        _drawVariables = new DrawVariables(_config);
        _uiIdentifiers = _config.ShouldHideOnUiObject
                                .Where(it => it.disable)
                                .SelectMany(it => it.getUiObjectIdentifier)
                                .ToArray();
        _currentUiObjectIndex = 0;
        return true;
    }

    // `GetAddonByName` is rather expensive, so we cache the ptr instead of
    // retrieving it each frame. As the `_NaviMap/AreaMap` object _almost_ never gets
    // reconstructed again, the ptr is valid almost the entire game.
    // Some cases when `_NaviMap` gets created anew:
    // - Aesthetician
    // - Any Deep Dungeon
    private bool UpdateMapPointersCache()
    {
        var ptr = _gameGui.GetAddonByName("_NaviMap");
        if (ptr == nint.Zero) return false;
        unsafe
        {
            var naviMap = (AtkUnitBase*)ptr;
            if (naviMap->UldManager.LoadedState != AtkLoadState.Loaded) return false;
            // Node indices valid as of 6.18
            var naviMapIconsRootComponentNode = (AtkComponentNode*)naviMap->UldManager.NodeList[2];
            var areaMap                       = (AtkUnitBase*)_gameGui.GetAddonByName("AreaMap");
            var areaMapIconsRootComponentNode = (AtkComponentNode*)areaMap->UldManager.NodeList[3];

            // Cardinals, etc. are on the same naviMap texture atlas
            var westCardinalAtkImageNode = (AtkImageNode*)naviMap->UldManager.NodeList[11];
            _pointers = new Pointers(
                TargetSystem.Instance(),
                (float*)((nint)naviMap + Constant.PlayerViewTriangleRotationOffset),
                _config.UseAreaMapAsSource ? areaMap : naviMap,
                _config.UseAreaMapAsSource ? areaMapIconsRootComponentNode : naviMapIconsRootComponentNode,
                (AtkImageNode*)((AtkComponentNode*)naviMap->UldManager.NodeList[6])->Component->UldManager.NodeList[2],
                new nint(
                    westCardinalAtkImageNode->PartsList->Parts[0]
                       .UldAsset->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
                )
            );
        }

        return true;
    }

    private void UpdateHideCompass()
    {
        // NOTE: We loop through max 8 at a time because else performance suffers too much,
        //  going through the whole list on each frame.
        //  It is better to take each frame roughly the same amount of time instead of having spikes,
        //  which is why this solution is used and not, e.g., only checking for open UI windows every 100ms or so.
        for (var i = 0; i < Math.Min(8, _uiIdentifiers.Length); i++)
        {
            var uiIdentifier = _uiIdentifiers[_currentUiObjectIndex++];
            _currentUiObjectIndex %= _uiIdentifiers.Length;
            if (_currentUiObjectIndex == 0)
            {
                _shouldHideCompass          = _shouldHideCompassIteration;
                _shouldHideCompassIteration = false;
            }

            var unitBase = _gameGui.GetAddonByName(uiIdentifier);
            if (unitBase == nint.Zero) continue;
            unsafe
            {
                _shouldHideCompassIteration |= ((AtkUnitBase*)unitBase)->IsVisible;
            }
        }
    }
}