using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin;

namespace Compass
{
    public unsafe partial class Compass
    {
        private ImGuiCompassData _imGuiCompassData =  new() 
        {
            PlayerPosition = new Vector2(ImGuiCompassData.NaviMapPlayerX, ImGuiCompassData.NaviMapPlayerY),
            ImGuiCompassCentre = new Vector2(835 + 125f, 515 + 25f),
            ImGuiCompassBackgroundPMin = Vector2.Zero,
            ImGuiCompassBackgroundPMax = Vector2.Zero,
            ImGuiCompassBackgroundLinePMin = Vector2.Zero,
            ImGuiCompassBackgroundLinePMax = Vector2.Zero,
            ImGuiCompassDrawListPMin = Vector2.Zero,
            ImGuiCompassDrawListPMax = Vector2.Zero,
            ImGuiCompassBackgroundDrawListPMin = Vector2.Zero,
            ImGuiCompassBackgroundDrawListPMax = Vector2.Zero,
            ImGuiCompassWeatherIconPMin = Vector2.Zero,
            ImGuiCompassWeatherIconPMax = Vector2.Zero,
            ImGuiCompassWeatherIconBorderPMin = Vector2.Zero,
            ImGuiCompassWeatherIconBorderPMax = Vector2.Zero,
            ImGuiCompassHalfWidth = 125f,
            ImGuiCompassHalfHeight = 125f,
            ImGuiCompassScale = 1f,
            CompassHeightScale = 1f,
            DistanceScaleFactorForRotationIcons = 1f,
            HalfWidth40 = 20f,
            HalfWidth28 = 14f,
            ImGuiCompassUnit = 0f,
            RotationIconHalfWidth = 12f,
            MinScaleFactor = 0.2f,
            MaxDistance = 180f,
            ImGuiBackgroundColourUInt32 = ImGuiCompassData.WhiteColor,
            ImGuiBackgroundBorderColourUInt32 = ImGuiCompassData.WhiteColor,
            ImGuiBackgroundLineColourUInt32 = ImGuiCompassData.WhiteColor,
            CurrentScaleOffset =  ImGuiCompassData.NaviMapScaleOffset
        };
        
        private AtkUnitBase* _naviMap = null!;
        private AtkUnitBase* _areaMap = null!;
        private AtkUnitBase* _currentSourceBase;
        private AtkComponentNode* _naviMapIconsRootComponentNode = null!;
        private AtkComponentNode* _areaMapIconsRootComponentNode = null!;
        private AtkComponentNode* _currentMapIconsRootComponentNode = null!;
        private AtkImageNode* _weatherIconNode = null!;
        private IntPtr _naviMapTextureD3D11ShaderResourceView;

        private int _componentIconLoopStart = 0;
        private int _iconLoopEnd = 0;

        private void UpdateMapAddonCache()
        {
            _naviMap = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            _naviMapIconsRootComponentNode = (AtkComponentNode*) _naviMap->UldManager.NodeList[2];
            _areaMap = (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("AreaMap", 1);
            _areaMapIconsRootComponentNode = (AtkComponentNode*) _areaMap->UldManager.NodeList[3];
            var westCardinalAtkImageNode = (AtkImageNode*) _naviMap->UldManager.NodeList[11];

            _naviMapTextureD3D11ShaderResourceView = new IntPtr(
                westCardinalAtkImageNode->PartsList->Parts[0]
                    .UldAsset->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
            );
            _weatherIconNode =(AtkImageNode*) ((AtkComponentNode*) _naviMap->UldManager.NodeList[6])->Component->UldManager.NodeList[2];
        }
        
        private void UpdateCompassVariables()
        {
            UpdateMapAddonCache();
            _imGuiCompassData.ImGuiCompassScale = _config.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
            _imGuiCompassData.CompassHeightScale = ImGui.GetIO().FontGlobalScale;

            _imGuiCompassData.DistanceScaleFactorForRotationIcons = _imGuiCompassData.ImGuiCompassScale * 0.7f;
            _imGuiCompassData.RotationIconHalfWidth = 16f * _imGuiCompassData.DistanceScaleFactorForRotationIcons;

            _imGuiCompassData.ImGuiCompassHalfWidth = _config.ImGuiCompassWidth * 0.5f;
            _imGuiCompassData.ImGuiCompassHalfHeight = ImGuiCompassData.ImGuiCompassHeight * 0.5f * _imGuiCompassData.CompassHeightScale;

            _imGuiCompassData.ImGuiCompassUnit = _config.ImGuiCompassWidth / (2f*(float)Math.PI);
            _imGuiCompassData.ImGuiCompassCentre =
                new Vector2(_config.ImGuiCompassPosition.X + _imGuiCompassData.ImGuiCompassHalfWidth,
                    _config.ImGuiCompassPosition.Y + _imGuiCompassData.ImGuiCompassHalfHeight);

            _imGuiCompassData.ImGuiCompassBackgroundPMin = new Vector2(
                _imGuiCompassData.ImGuiCompassCentre.X - 5 - _imGuiCompassData.ImGuiCompassHalfWidth * _config.ImGuiCompassReverseMaskPercentage
                , _imGuiCompassData.ImGuiCompassCentre.Y - _imGuiCompassData.ImGuiCompassHalfHeight * 0.5f - 2
                );
            _imGuiCompassData.ImGuiCompassBackgroundPMax = new Vector2(
                _imGuiCompassData.ImGuiCompassCentre.X + 5 + _imGuiCompassData.ImGuiCompassHalfWidth * _config.ImGuiCompassReverseMaskPercentage
                , _imGuiCompassData.ImGuiCompassCentre.Y + _imGuiCompassData.ImGuiCompassHalfHeight * 0.5f + 2
                );
            _imGuiCompassData.ImGuiCompassBackgroundLinePMin = new Vector2(
                _imGuiCompassData.ImGuiCompassBackgroundPMin.X
                , _config.ImGuiCompassBackgroundLineOffset + _imGuiCompassData.ImGuiCompassBackgroundPMin.Y + _imGuiCompassData.ImGuiCompassHalfHeight
                );
            _imGuiCompassData.ImGuiCompassBackgroundLinePMax = new Vector2(
                _imGuiCompassData.ImGuiCompassBackgroundPMax.X,
                _config.ImGuiCompassBackgroundLineOffset + _imGuiCompassData.ImGuiCompassBackgroundPMin.Y + _imGuiCompassData.ImGuiCompassHalfHeight
                );

            _imGuiCompassData.ImGuiCompassDrawListPMin =
                _imGuiCompassData.ImGuiCompassBackgroundPMin + new Vector2(-2, -100);
            _imGuiCompassData.ImGuiCompassDrawListPMax = 
                _imGuiCompassData.ImGuiCompassBackgroundPMax + new Vector2(2, 100);
            _imGuiCompassData.ImGuiCompassBackgroundDrawListPMin =
                _imGuiCompassData.ImGuiCompassBackgroundPMin + new Vector2(-3, -100);
            _imGuiCompassData.ImGuiCompassBackgroundDrawListPMax =
                _imGuiCompassData.ImGuiCompassBackgroundPMax + new Vector2(3, 100);

            _imGuiCompassData.ImGuiCompassWeatherIconPMin =
                _imGuiCompassData.ImGuiCompassBackgroundPMin + _config.ImGuiCompassWeatherIconOffset + new Vector2(2, 1) * _config.ImGuiCompassWeatherIconScale;
            _imGuiCompassData.ImGuiCompassWeatherIconPMax = 
                _imGuiCompassData.ImGuiCompassWeatherIconPMin + new Vector2(32, 32) * _config.ImGuiCompassWeatherIconScale;
            
            _imGuiCompassData.ImGuiCompassWeatherIconBorderPMin = _config.ShowWeatherIconBorder
                ? _imGuiCompassData.ImGuiCompassBackgroundPMin + _config.ImGuiCompassWeatherIconOffset 
                : Vector2.Zero;
            _imGuiCompassData.ImGuiCompassWeatherIconBorderPMax = _config.ShowWeatherIconBorder
                 ? _imGuiCompassData.ImGuiCompassWeatherIconBorderPMin + new Vector2(36, 36)* _config.ImGuiCompassWeatherIconScale
                 : Vector2.Zero;
                            
            _imGuiCompassData.HalfWidth40 = 20 * _imGuiCompassData.ImGuiCompassScale;
            _imGuiCompassData.HalfWidth28 = 14 * _imGuiCompassData.ImGuiCompassScale;

            _imGuiCompassData.ImGuiBackgroundColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundColour);
            _imGuiCompassData.ImGuiBackgroundBorderColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundBorderColour);
            _imGuiCompassData.ImGuiBackgroundLineColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundLineColour);

            _imGuiCompassData.MinScaleFactor = _config.UseAreaMapAsSource ? 0 : 0.2f;
            _imGuiCompassData.MaxDistance = _config.UseAreaMapAsSource ? _config.AreaMapMaxDistance : 180f;
            _imGuiCompassData.CurrentScaleOffset = _config.UseAreaMapAsSource
                ? ImGuiCompassData.AreaMapScaleOffset
                : ImGuiCompassData.NaviMapScaleOffset;
            _currentSourceBase = _config.UseAreaMapAsSource ? _areaMap : _naviMap;
            _currentMapIconsRootComponentNode = _config.UseAreaMapAsSource
                ? _areaMapIconsRootComponentNode
                : _naviMapIconsRootComponentNode;
            _componentIconLoopStart = _config.UseAreaMapAsSource ? 7 : 4;
            _iconLoopEnd = _config.UseAreaMapAsSource ? 7 : 6;

            _uiIdentifiers = _config.ShouldHideOnUiObject
                .Where(it => it.disable)
                .SelectMany(it => it.getUiObjectIdentifier)
                .ToArray();
            _currentUiObjectIndex = 0;
        }

        private void BuildImGuiCompass()
        {
            if (!_config.ImGuiCompassEnable) return;
            if (_config.HideInCombat && _pluginInterface.ClientState.Condition[ConditionFlag.InCombat]) return;
            UpdateHideCompass();
            if (_shouldHideCompass) return;
#if DEBUG
            if (_currentSourceBase == null)
            {
                SimpleLog.Error($"{nameof(_currentSourceBase)} is null!");
                return;
            }
#endif
            if (_currentSourceBase->UldManager.LoadedState != 3) return;
            if (!_currentSourceBase->IsVisible) return;
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
                                           | ImGuiWindowFlags.NoMove
                                           | ImGuiWindowFlags.NoMouseInputs
                                           | ImGuiWindowFlags.NoFocusOnAppearing
                                           | ImGuiWindowFlags.NoBackground
                                           | ImGuiWindowFlags.NoNav
                                           | ImGuiWindowFlags.NoInputs
                                           | ImGuiWindowFlags.NoCollapse
                                           | ImGuiWindowFlags.NoSavedSettings;
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(_config.ImGuiCompassPosition, ImGuiCond.Always);
            if (!ImGui.Begin("###ImGuiCompassWindow", flags)
            )
            {
                ImGui.End();
                return;
            }
            var drawList = ImGui.GetWindowDrawList();
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            drawList.PushClipRect(
                _imGuiCompassData.ImGuiCompassDrawListPMin
                , _imGuiCompassData.ImGuiCompassDrawListPMax
                );
            backgroundDrawList.PushClipRect(
                _imGuiCompassData.ImGuiCompassBackgroundDrawListPMin
                , _imGuiCompassData.ImGuiCompassBackgroundDrawListPMax
                );
            const uint playerViewTriangleRotationOffset = 0x254;
            // 0 == Facing North, -PI/2 facing east, PI/2 facing west.
            //var cameraRotationInRadian = *(float*) (_maybeCameraStruct + 0x130);
            //var _miniMapIconsRootComponentNode = (AtkComponentNode*)_naviMap->ULDData.NodeList[2];
            // Minimap rotation thingy is even already flipped!
            // And apparently even accessible & updated if _NaviMap is disabled
            // => This leads to jerky behaviour though
            var cameraRotationInRadian = *(float*)((nint)_naviMap + playerViewTriangleRotationOffset) * Deg2Rad;
            
            var cosPlayer = (float) Math.Cos(cameraRotationInRadian);
            var sinPlayer = (float) Math.Sin(cameraRotationInRadian);
            // NOTE (Chiv) Interpret game's camera rotation as
            // 0 => (0,1) (North), PI/2 => (-1,0) (West)  in default coordinate system
            // Games Map coordinate system origin is upper left, with positive Y grow
            var playerForward = new Vector2(-sinPlayer, cosPlayer);
            
            // First, the background
            DrawImGuiCompassBackground();
            // Second, we position our Cardinals
            DrawCardinals(playerForward);
            if (_config.ShowOnlyCardinals) return;
            if (_config.ShowWeatherIcon) DrawWeatherIcon();
            DrawCompassIcons(playerForward);
            drawList.PopClipRect();
            backgroundDrawList.PopClipRect();
            ImGui.End();
        }

        private void DrawWeatherIcon()
        {
            if (!_weatherIconNode->AtkResNode.IsVisible) return;
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            backgroundDrawList.PushClipRectFullScreen();
            
            //Background of Weather Icon
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , _imGuiCompassData.ImGuiCompassWeatherIconBorderPMin
                , _imGuiCompassData.ImGuiCompassWeatherIconBorderPMax
                , new Vector2(0.08035714f, 0.8301887f)
                , new Vector2(0.1607143f, 1));
            //Weather Icon
            var tex = _weatherIconNode->PartsList->Parts[0].UldAsset->AtkTexture.Resource->KernelTextureObject;
            backgroundDrawList.AddImage(
                new IntPtr(tex->D3D11ShaderResourceView), _imGuiCompassData.ImGuiCompassWeatherIconPMin, _imGuiCompassData.ImGuiCompassWeatherIconPMax);
            //Border around Weather Icon
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , _imGuiCompassData.ImGuiCompassWeatherIconBorderPMin
                , _imGuiCompassData.ImGuiCompassWeatherIconBorderPMax
                , new Vector2(0.1607143f, 0.8301887f)
                , new Vector2(0.2410714f, 1));
            
            backgroundDrawList.PopClipRect();

        }
        
        private void DrawCompassIcons(Vector2 playerForward)
        {
            try
            {
                var drawList = ImGui.GetWindowDrawList();
                // Then, we do the dance through all relevant nodes on _NaviMap or AreaMap, depending on the setting
                // I imagine this throws sometimes because of racing conditions -> We try to access an already freed texture e.g.
                // So we just ignore those small exceptions, it works a few frames later anyways
                var mapScale = *(float*) ((long) _currentSourceBase + _imGuiCompassData.CurrentScaleOffset);
                var distanceOffset = 20f * mapScale;  
                var maxDistance = _imGuiCompassData.MaxDistance * mapScale;
                for (var i = _componentIconLoopStart; i < _currentMapIconsRootComponentNode->Component->UldManager.NodeListCount ; i++)
                {
                    var mapIconComponentNode =
                        (AtkComponentNode*) _currentMapIconsRootComponentNode->Component->UldManager.NodeList[i];
                    if (!mapIconComponentNode->AtkResNode.IsVisible) continue;
                    for (var j = 2; j < _iconLoopEnd; j++)
                    {
                        var imgNode = (AtkImageNode*) mapIconComponentNode->Component->UldManager.NodeList[j];
                        if (imgNode->AtkResNode.Type != NodeType.Image) continue;
                        if (!imgNode->AtkResNode.IsVisible || !imgNode->AtkResNode.ParentNode->IsVisible) continue;
                        var part = imgNode->PartsList->Parts[imgNode->PartId];
                        //NOTE (CHIV) Invariant: It should always be a resource
#if DEBUG
                        var type = part.UldAsset->AtkTexture.TextureType;
                        if (type != TextureType.Resource)
                        {
                            SimpleLog.Error($"{i} {j} was not a Resource texture");
                            continue;
                        };
#endif
                        var tex = part.UldAsset->AtkTexture.Resource->KernelTextureObject;
                        var texFileNamePtr =
                            part.UldAsset->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        // NOTE (Chiv) We are in a try-catch, so we just throw if the read failed.
                        // Cannot act anyways if the texture path is butchered
                        var textureFileName = new string((sbyte*) texFileNamePtr);
                        var _ = uint.TryParse(
                            textureFileName.Substring(textureFileName.LastIndexOfAny(new[] {'/', '\\'}) + 1, 6),
                            out var iconId);
                        //iconId = 0 (==> success == false as IconID will never be 0) Must have been 'NaviMap(_hr1)?\.tex' (and only that hopefully)
                        if (_config.FilteredIconIds.Contains(iconId)) continue;
                        
                        var textureIdPtr = new IntPtr(tex->D3D11ShaderResourceView);
                        Vector2 pMin;
                        Vector2 pMax;
                        var uv = Vector2.Zero;
                        var uv1 = Vector2.One;
                        var tintColour = ImGuiCompassData.WhiteColor;
                        var rotate = false;
                        switch (iconId)
                        {
                            case 0 when _config.UseAreaMapAsSource:
                                continue;
                            case 0 when imgNode->PartId == 21: //Glowy thingy
                                rotate = true; // TODO(Chiv) Duplicate code instead of branching?
                                uv = new Vector2( (float) part.U / 448, (float) part.V / 212);
                                uv1 = new Vector2( (float) (part.U + 40) / 448, (float) (part.V + 40) / 212);
                                // NOTE (Chiv) Glowy thingy always rotates, but whether its in or outside the mask
                                // determines how to calculate its position on the compass
                                if (mapIconComponentNode->AtkResNode.Rotation == 0)
                                    goto default;
                                goto case 1;
                            case 0: //Arrows to quests and fates
                                // NOTE (Chiv) We assume part.Width == part.Height == 24
                                // NOTE (Chiv) We assume tex.Width == 448 && tex.Height == 212
                                var u = (float) part.U / 448; // = (float) part.U / tex->Width;
                                var v = (float) part.V / 212; // = (float) part.V / tex->Height;
                                var u1 = (float) (part.U + 24) / 448; // = (float) (part.U + part.Width) / tex->Width;
                                var v1 = (float) (part.V + 24) / 212; // = (float) (part.V + part.Height) / tex->Height;
                                //var u = (float) part.U / tex->Width;
                                //var v = (float) part.V / tex->Height; 
                                //var u1 = (float) (part.U + part.Width) / tex->Width; 
                                //var v1 = (float) (part.V + part.Height) / tex->Height;

                                uv = new Vector2(u, v);
                                uv1 = new Vector2(u1, v1);
                                // Arrows and such are always rotation based, we draw them slightly on top
                                var naviMapCutIconOffset = _imGuiCompassData.ImGuiCompassUnit *
                                                           SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                               playerForward);
                                // We hope width == height
                                const int naviMapIconHalfWidth = 12;
                                var naviMapYOffset = 14 * _imGuiCompassData.ImGuiCompassScale;
                                pMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - naviMapIconHalfWidth + naviMapCutIconOffset, _imGuiCompassData.ImGuiCompassCentre.Y - naviMapYOffset - naviMapIconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + naviMapCutIconOffset + naviMapIconHalfWidth, _imGuiCompassData.ImGuiCompassCentre.Y - naviMapYOffset + naviMapIconHalfWidth);
                                break;
                            case 1: // Rotation icons (except naviMap arrows) go here after setting up their UVs
                                // NOTE (Chiv) Rotations for icons on the map are mirrored from the
                                var rotationIconOffset = _imGuiCompassData.ImGuiCompassUnit *
                                                         SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                             playerForward);
                                pMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.RotationIconHalfWidth + rotationIconOffset, _imGuiCompassData.ImGuiCompassCentre.Y - _imGuiCompassData.RotationIconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + rotationIconOffset + _imGuiCompassData.RotationIconHalfWidth, _imGuiCompassData.ImGuiCompassCentre.Y + _imGuiCompassData.RotationIconHalfWidth);
                                break;
                            case 060443: //Player Marker
                                if (!_config.ImGuiCompassEnableCenterMarker) continue;
                                drawList = ImGui.GetBackgroundDrawList();
                                pMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth40, _imGuiCompassData.ImGuiCompassCentre.Y + _config.ImGuiCompassCentreMarkerOffset * _imGuiCompassData.ImGuiCompassScale - _imGuiCompassData.HalfWidth40);
                                pMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + _imGuiCompassData.HalfWidth40, _imGuiCompassData.ImGuiCompassCentre.Y + _config.ImGuiCompassCentreMarkerOffset * _imGuiCompassData.ImGuiCompassScale + _imGuiCompassData.HalfWidth40);
                                uv1 = _config.ImGuiCompassFlipCentreMarker ? new Vector2(1, -1) : Vector2.One;
                                _imGuiCompassData.PlayerPosition = new Vector2(mapIconComponentNode->AtkResNode.X, -mapIconComponentNode->AtkResNode.Y);
                                break;
                            //case  >5 and <9 :
                            //    break;
                            case 060495: // Small Area Circle
                            case 060496: // Big Area Circle
                            case 060497: // Another Circle
                            case 060498: // One More Circle
                                bool inArea;
                                (pMin, pMax, tintColour, inArea)
                                    = CalculateAreaCirlceVariables(_imGuiCompassData.PlayerPosition, playerForward, mapIconComponentNode,
                                        imgNode, distanceOffset, _imGuiCompassData.ImGuiCompassUnit, _imGuiCompassData.HalfWidth40, _imGuiCompassData.ImGuiCompassCentre,
                                        maxDistance, _imGuiCompassData.MinScaleFactor);
                                if (inArea)
                                    //*((byte*) &tintColour + 3) = 0x33  == (0x33FFFFFF) & (tintColour)
                                    ImGui.GetBackgroundDrawList().AddRectFilled(_imGuiCompassData.ImGuiCompassBackgroundPMin
                                        , _imGuiCompassData.ImGuiCompassBackgroundPMax
                                        , 0x33FFFFFF & tintColour //Set A to 0.2
                                        , _config.ImGuiCompassBackgroundRounding
                                    );
                                break;
                            case 060541: // Arrow UP on Circle
                            case 060542: // Arrow UP on Circle
                            case 060543: // Another Arrow UP
                            case 060545: // Another Arrow DOWN
                            case 060546: // Arrow DOWN on Circle
                            case 060547: // Arrow DOWN on Circle
                                (pMin, pMax, tintColour, _)
                                    = CalculateAreaCirlceVariables(_imGuiCompassData.PlayerPosition, playerForward, mapIconComponentNode,
                                        imgNode, distanceOffset, _imGuiCompassData.ImGuiCompassUnit, _imGuiCompassData.HalfWidth40, _imGuiCompassData.ImGuiCompassCentre,
                                        maxDistance, _imGuiCompassData.MinScaleFactor);
                                break;
                            case 071003: // MSQ Ongoing Marker
                            case 071005: // MSQ Complete Marker
                            case 071023: // Quest Ongoing Marker
                            case 071033: // Quest Ongoing Red Marker
                            case 071025: // Quest Complete Marker
                            case 071035: // Quest Complete Red Marker
                            case 071063: // BookQuest Ongoing Marker
                            case 071065: // BookQuest Complete Marker
                            case 071083: // LeveQuest Ongoing Marker
                            case 071085: // LeveQuest Complete Marker
                            case 071143: // BlueQuest Ongoing Marker
                            case 071145: // BlueQuest Complete Marker
                            case 071146: // Weird Blueish Round Exclamation Mark //TODO More of those?
                            case 071026: // Weird Blueish Round Exclamation Mark 2 
                            case 071111: // Weird Round Quest Mark  
                            case 071112: // Weird Round Quest Complete Mark
                            case 060954: // Arrow up for quests
                            case 060955: // Arrow down for quests
                            case 060561: // Red Flag (Custom marker)
                            case 060437: // Mining Crop Icon  
                            case 060438: // Mining Crop Icon 2
                            case 060432: // Botanic Crop Icon 
                            case 060433: // Botanic Crop Icon 2 
                                if (mapIconComponentNode->AtkResNode.Rotation == 0)
                                    // => The current quest marker is inside the mask and should be
                                    // treated as a map point
                                    goto default;
                                // => The current quest marker is outside the mask and should be
                                // treated as a rotation
                                goto case 1; //No UV setup needed for quest markers
                            default:
                                // NOTE (Chiv) Remember, Y needs to be flipped to transform to default coordinate system
                                var (distanceScaleFactor, iconAngle, _) = CalculateDrawVariables(_imGuiCompassData.PlayerPosition,
                                    new Vector2(
                                        mapIconComponentNode->AtkResNode.X,
                                        -mapIconComponentNode->AtkResNode.Y
                                    ),
                                    playerForward,
                                    distanceOffset,
                                    maxDistance,
                                    _imGuiCompassData.MinScaleFactor
                                );
                                var iconOffset = _imGuiCompassData.ImGuiCompassUnit * iconAngle;
                                var iconHalfWidth = _imGuiCompassData.HalfWidth40 * distanceScaleFactor;
                                pMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - iconHalfWidth + iconOffset, _imGuiCompassData.ImGuiCompassCentre.Y - iconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + iconOffset + iconHalfWidth, _imGuiCompassData.ImGuiCompassCentre.Y + iconHalfWidth);
                                break;
                        }

                        if (rotate)
                            ImageRotated(textureIdPtr,
                                new Vector2(pMin.X + (pMax.X - pMin.X)*0.5f, pMin.Y + (pMax.Y - pMin.Y)*0.5f)
                                ,pMax - pMin, imgNode->AtkResNode.Rotation, uv, uv1, drawList);
                        else
                            drawList.AddImage(textureIdPtr, pMin, pMax, uv, uv1, tintColour);
                    }
                }
            }
#if DEBUG
            catch (Exception e)
            {

                SimpleLog.Error(e);
#else
            catch
            {
                // ignored
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ImageRotated(IntPtr texId, Vector2 center, Vector2 size, float angle, Vector2 uv, Vector2 uv1, ImDrawListPtr? drawList = null)
        {
            
            drawList ??= ImGui.GetWindowDrawList();
            
            var cosA = (float)Math.Cos(angle);
            var sinA = (float)Math.Sin(angle);
            var pos = stackalloc[]
            {
                center + Rotate(new Vector2(-size.X * 0.5f, -size.Y * 0.5f), cosA, sinA),
                center + Rotate(new Vector2(+size.X * 0.5f, -size.Y * 0.5f), cosA, sinA),
                center + Rotate(new Vector2(+size.X * 0.5f, +size.Y * 0.5f), cosA, sinA),
                center + Rotate(new Vector2(-size.X * 0.5f, +size.Y * 0.5f), cosA, sinA)
                
            };
            var uvs = stackalloc[] 
            { 
                new Vector2(uv.X, uv.Y),
                new Vector2(uv1.X, uv.Y),
                new Vector2(uv1.X, uv1.Y),
                new Vector2(uv.X, uv1.Y)
            };
            drawList.Value.AddImageQuad(texId, pos[0], pos[1], pos[2], pos[3], uvs[0], uvs[1], uvs[2], uvs[3]);
        }

        private void DrawCardinals(Vector2 playerForward)
        {
            var backgroundDrawList = ImGui.GetBackgroundDrawList();

            var east = Vector2.UnitX;
            var south = -Vector2.UnitY;
            var west = -Vector2.UnitX;
            var north = Vector2.UnitY;
            var eastOffset = _imGuiCompassData.ImGuiCompassUnit * SignedAngle(east, playerForward);
            var pMinY = _imGuiCompassData.ImGuiCompassCentre.Y - _imGuiCompassData.HalfWidth40 + _config.ImGuiCompassCardinalsOffset;
            var pMaxY = _imGuiCompassData.ImGuiCompassCentre.Y + _imGuiCompassData.HalfWidth40 + _config.ImGuiCompassCardinalsOffset;
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth28 + eastOffset, pMinY)
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + eastOffset + _imGuiCompassData.HalfWidth28, pMaxY)
                , new Vector2(0.5446429f, 0.8301887f)
                , new Vector2(0.5892857f, 0.9811321f)
            );
            var southOffset = _imGuiCompassData.ImGuiCompassUnit * SignedAngle(south, playerForward);
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth28 + southOffset, pMinY)
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + southOffset + _imGuiCompassData.HalfWidth28, pMaxY)
                , new Vector2(0.5892857f, 0.8301887f)
                , new Vector2(0.6339286f, 0.9811321f)
            );
            var westOffset = _imGuiCompassData.ImGuiCompassUnit * SignedAngle(west, playerForward);
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth40 + westOffset, pMinY)
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + westOffset + _imGuiCompassData.HalfWidth40, pMaxY)
                , new Vector2(0.4732143f, 0.8301887f)
                , new Vector2(0.5446429f, 0.9811321f)
            );
            var northOffset = _imGuiCompassData.ImGuiCompassUnit * SignedAngle(north, playerForward);
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth40 + northOffset, pMinY)
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + northOffset + _imGuiCompassData.HalfWidth40, pMaxY)
                , new Vector2(0.4017857f, 0.8301887f)
                , new Vector2(0.4732143f, 0.9811321f)
                , 0xFF0064B0 //ABGR ImGui.ColorConvertFloat4ToU32(new Vector4(176f / 255f, 100f / 255f, 0f, 1))
            );
        }
        
        private void DrawImGuiCompassBackground()
        {
            if (!_config.ImGuiCompassEnableBackground) return;
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            if (_config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Filled or ImGuiCompassBackgroundStyle.FilledAndBorder)
                backgroundDrawList.AddRectFilled(_imGuiCompassData.ImGuiCompassBackgroundPMin
                    , _imGuiCompassData.ImGuiCompassBackgroundPMax
                    , _imGuiCompassData.ImGuiBackgroundColourUInt32
                    , _config.ImGuiCompassBackgroundRounding
                );
            if (_config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Border or ImGuiCompassBackgroundStyle.FilledAndBorder)
                backgroundDrawList.AddRect(_imGuiCompassData.ImGuiCompassBackgroundPMin - Vector2.One
                    , _imGuiCompassData.ImGuiCompassBackgroundPMax + Vector2.One
                    , _imGuiCompassData.ImGuiBackgroundBorderColourUInt32
                    , _config.ImGuiCompassBackgroundRounding
                    , ImDrawFlags.RoundCornersAll
                    , _config.ImGuiBackgroundBorderThickness
                );
            if (_config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Line)
                backgroundDrawList.AddLine(_imGuiCompassData.ImGuiCompassBackgroundLinePMin
                    , _imGuiCompassData.ImGuiCompassBackgroundLinePMax
                    , _imGuiCompassData.ImGuiBackgroundLineColourUInt32
                    , _config.ImGuiBackgroundLineThickness
                    );
        }
        
        private void UpdateHideCompass()
        {
            for (var i = 0; i < Math.Min(8, _uiIdentifiers.Length); i++)
            {
                var uiIdentifier = _uiIdentifiers[_currentUiObjectIndex++];
                _currentUiObjectIndex %= _uiIdentifiers.Length;
                if (_currentUiObjectIndex == 0)
                {
                    _shouldHideCompass = _shouldHideCompassIteration;
                    _shouldHideCompassIteration = false;
                }
                var unitBase = _pluginInterface.Framework.Gui.GetUiObjectByName(uiIdentifier, 1);
                if (unitBase == IntPtr.Zero) continue;
                _shouldHideCompassIteration |= ((AtkUnitBase*) unitBase)->IsVisible;
            }
        }
    }
}