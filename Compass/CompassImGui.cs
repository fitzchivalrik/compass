using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace Compass
{
    public unsafe partial class Compass
    {
        private ImGuiCompassData _imGuiCompassData =  new() 
        {
            PlayerPosition = new Vector2(ImGuiCompassData.NaviMapPlayerX, ImGuiCompassData.NaviMapPlayerY),
            Centre = new Vector2(835 + 125f, 515 + 25f),
            BackgroundPMin = Vector2.Zero,
            BackgroundPMax = Vector2.Zero,
            BackgroundLinePMin = Vector2.Zero,
            BackgroundLinePMax = Vector2.Zero,
            DrawListPMin = Vector2.Zero,
            DrawListPMax = Vector2.Zero,
            BackgroundDrawListPMin = Vector2.Zero,
            BackgroundDrawListPMax = Vector2.Zero,
            WeatherIconPMin = Vector2.Zero,
            WeatherIconPMax = Vector2.Zero,
            WeatherIconBorderPMin = Vector2.Zero,
            WeatherIconBorderPMax = Vector2.Zero,
            DistanceToTargetPMin = Vector2.Zero,
            HalfWidth = 125f,
            HalfHeight = 125f,
            Scale = 1f,
            HeightScale = 1f,
            DistanceScaleFactorForRotationIcons = 1f,
            HalfWidth40 = 20f,
            HalfWidth28 = 14f,
            CompassUnit = 0f,
            RotationIconHalfWidth = 12f,
            MinScaleFactor = 0.2f,
            MaxDistance = 180f,
            DistanceToTargetScale = 1f,
            BackgroundColourUInt32 = ImGuiCompassData.WhiteColor,
            BackgroundBorderColourUInt32 = ImGuiCompassData.WhiteColor,
            BackgroundLineColourUInt32 = ImGuiCompassData.WhiteColor,
            DistanceToTargetColourUInt32 = ImGuiCompassData.WhiteColor,
            CurrentScaleOffset =  ImGuiCompassData.NaviMapScaleOffset,
        };

        private readonly TargetSystem* _targetSystem;
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
            _naviMap = (AtkUnitBase*) _gameGui.GetAddonByName("_NaviMap", 1);
            _naviMapIconsRootComponentNode = (AtkComponentNode*) _naviMap->UldManager.NodeList[2];
            _areaMap = (AtkUnitBase*) _gameGui.GetAddonByName("AreaMap", 1);
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
            _imGuiCompassData.Scale = _config.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
            _imGuiCompassData.HeightScale = ImGui.GetIO().FontGlobalScale;

            _imGuiCompassData.DistanceScaleFactorForRotationIcons = _imGuiCompassData.Scale * 0.7f;
            _imGuiCompassData.RotationIconHalfWidth = 16f * _imGuiCompassData.DistanceScaleFactorForRotationIcons;

            _imGuiCompassData.HalfWidth = _config.ImGuiCompassWidth * 0.5f;
            _imGuiCompassData.HalfHeight = ImGuiCompassData.Height * 0.5f * _imGuiCompassData.HeightScale;

            _imGuiCompassData.CompassUnit = _config.ImGuiCompassWidth / (2f*(float)Math.PI);
            _imGuiCompassData.Centre =
                new Vector2(_config.ImGuiCompassPosition.X + _imGuiCompassData.HalfWidth,
                    _config.ImGuiCompassPosition.Y + _imGuiCompassData.HalfHeight);

            _imGuiCompassData.BackgroundPMin = new Vector2(
                _imGuiCompassData.Centre.X - 5 - _imGuiCompassData.HalfWidth * _config.ImGuiCompassReverseMaskPercentage
                , _imGuiCompassData.Centre.Y - _imGuiCompassData.HalfHeight * 0.5f - 2
                );
            _imGuiCompassData.BackgroundPMax = new Vector2(
                _imGuiCompassData.Centre.X + 5 + _imGuiCompassData.HalfWidth * _config.ImGuiCompassReverseMaskPercentage
                , _imGuiCompassData.Centre.Y + _imGuiCompassData.HalfHeight * 0.5f + 2
                );
            _imGuiCompassData.BackgroundLinePMin = new Vector2(
                _imGuiCompassData.BackgroundPMin.X
                , _config.ImGuiCompassBackgroundLineOffset + _imGuiCompassData.BackgroundPMin.Y + _imGuiCompassData.HalfHeight
                );
            _imGuiCompassData.BackgroundLinePMax = new Vector2(
                _imGuiCompassData.BackgroundPMax.X,
                _config.ImGuiCompassBackgroundLineOffset + _imGuiCompassData.BackgroundPMin.Y + _imGuiCompassData.HalfHeight
                );
            _imGuiCompassData.DistanceToTargetPMin = _imGuiCompassData.Centre + _config.ImGuiCompassDistanceToTargetOffset;
            _imGuiCompassData.DistanceToTargetScale =
                ImGui.GetIO().FontGlobalScale * _config.ImGuiCompassDistanceToTargetScale;
            _imGuiCompassData.DrawListPMin =
                _imGuiCompassData.BackgroundPMin + new Vector2(-2, -100);
            _imGuiCompassData.DrawListPMax = 
                _imGuiCompassData.BackgroundPMax + new Vector2(2, 100);
            _imGuiCompassData.BackgroundDrawListPMin =
                _imGuiCompassData.BackgroundPMin + new Vector2(-3, -100);
            _imGuiCompassData.BackgroundDrawListPMax =
                _imGuiCompassData.BackgroundPMax + new Vector2(3, 100);

            _imGuiCompassData.WeatherIconPMin =
                _imGuiCompassData.BackgroundPMin + _config.ImGuiCompassWeatherIconOffset + new Vector2(2, 1) * _config.ImGuiCompassWeatherIconScale;
            _imGuiCompassData.WeatherIconPMax = 
                _imGuiCompassData.WeatherIconPMin + new Vector2(32, 32) * _config.ImGuiCompassWeatherIconScale;
            
            _imGuiCompassData.WeatherIconBorderPMin = _config.ShowWeatherIconBorder
                ? _imGuiCompassData.BackgroundPMin + _config.ImGuiCompassWeatherIconOffset 
                : Vector2.Zero;
            _imGuiCompassData.WeatherIconBorderPMax = _config.ShowWeatherIconBorder
                 ? _imGuiCompassData.WeatherIconBorderPMin + new Vector2(36, 36)* _config.ImGuiCompassWeatherIconScale
                 : Vector2.Zero;
                            
            _imGuiCompassData.HalfWidth40 = 20 * _imGuiCompassData.Scale;
            _imGuiCompassData.HalfWidth28 = 14 * _imGuiCompassData.Scale;

            _imGuiCompassData.BackgroundColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundColour);
            _imGuiCompassData.BackgroundBorderColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundBorderColour);
            _imGuiCompassData.BackgroundLineColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundLineColour);
            _imGuiCompassData.DistanceToTargetColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiCompassDistanceToTargetColour);

            _imGuiCompassData.MinScaleFactor = _config.UseAreaMapAsSource ? _config.ImGuiCompassMinimumIconScaleFactorAreaMap : _config.ImGuiCompassMinimumIconScaleFactor;
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

        private void DrawImGuiCompass()
        {
            if (!_config.ImGuiCompassEnable) return;
            if (_config.HideInCombat && _condition[ConditionFlag.InCombat]) return;
            UpdateHideCompass();
            if (_shouldHideCompass) return;
#if DEBUG
            if (_currentSourceBase == null)
            {
                SimpleLog.Error($"{nameof(_currentSourceBase)} is null!");
                return;
            }
#endif
            const uint playerViewTriangleRotationOffset = 0x254;
            float cameraRotationInRadian;
            try
            {
                if (_currentSourceBase->UldManager.LoadedState != 3) return;
                if (!_currentSourceBase->IsVisible) return;
                // 0 == Facing North, -PI/2 facing east, PI/2 facing west.
                //var cameraRotationInRadian = *(float*) (_maybeCameraStruct + 0x130);
                //var _miniMapIconsRootComponentNode = (AtkComponentNode*)_naviMap->ULDData.NodeList[2];
                // Minimap rotation thingy is even already flipped!
                // And apparently even accessible & updated if _NaviMap is disabled
                // => This leads to jerky behaviour though
                cameraRotationInRadian = *(float*) ((nint) _naviMap + playerViewTriangleRotationOffset) * Deg2Rad;
            }
#if DEBUG
            catch (Exception e)
            {

                SimpleLog.Error(e);
                return;
#else
            catch
            {
                // ignored
                return;
#endif
            }
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
            
            var cosPlayer = (float) Math.Cos(cameraRotationInRadian);
            var sinPlayer = (float) Math.Sin(cameraRotationInRadian);
            // NOTE (Chiv) Interpret game's camera rotation as
            // 0 => (0,1) (North), PI/2 => (-1,0) (West)  in default coordinate system
            // Games Map coordinate system origin is upper left, with positive Y grow
            var playerForward = new Vector2(-sinPlayer, cosPlayer);
            var drawList = ImGui.GetWindowDrawList();
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            drawList.PushClipRect(
                _imGuiCompassData.DrawListPMin
                , _imGuiCompassData.DrawListPMax
                );
            backgroundDrawList.PushClipRect(
                _imGuiCompassData.BackgroundDrawListPMin
                , _imGuiCompassData.BackgroundDrawListPMax
                );
            
            // First, the background
            DrawImGuiCompassBackground();
            // Second, we position our Cardinals
            DrawCardinals(playerForward);
            if (_config.ShowWeatherIcon) DrawWeatherIcon();
            if (_config.ShowDistanceToTarget) DrawDistanceToTarget();
            if (!_config.ShowOnlyCardinals)
            {
                DrawCompassIcons(playerForward);
            }
            drawList.PopClipRect();
            backgroundDrawList.PopClipRect();
            ImGui.End();
        }

        private void DrawDistanceToTarget()
        {
            var drawList = ImGui.GetWindowDrawList();
            var current = _config.ImGuiCompassDistanceToTargetMouseOverPrio && _targetSystem->MouseOverTarget != null
                ? _targetSystem->MouseOverTarget
                : _targetSystem->GetCurrentTarget();
            if (current is null) return;
            var distanceFromPlayer = *current switch
            {
                { ObjectKind: var o } when
                    (ObjectKind)o is ObjectKind.Pc or ObjectKind.BattleNpc or ObjectKind.EventNpc
                    => current->YalmDistanceFromPlayerX,
                _ => byte.MaxValue,
            };
            if (distanceFromPlayer == byte.MaxValue) return;
            var text = $"{_config.DistanceToTargetPrefix}{distanceFromPlayer + 1}{_config.DistanceToTargetSuffix}";
            var font = UiBuilder.DefaultFont;
            var distanceToTargetScale = font.FontSize * _imGuiCompassData.DistanceToTargetScale;
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin + new Vector2(-1,+1)
                , 0xFF000000,
                text);
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin + new Vector2(0,+1)
                , 0xFF000000
                , text);
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin + new Vector2(+1,+1)
                , 0xFF000000,
                text);
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin + new Vector2(-1,0)
                , 0xFF000000,
                text);
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin + new Vector2(+1,0)
                , 0xFF000000,
                text);
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin + new Vector2(-1,-1)
                , 0xFF000000,
                text);
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin + new Vector2(0,-1)
                , 0xFF000000,
                text);
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin + new Vector2(+1,-1)
                , 0xFF000000,
                text);
            drawList.AddText(font,
                distanceToTargetScale,
                _imGuiCompassData.DistanceToTargetPMin
                , _imGuiCompassData.DistanceToTargetColourUInt32,
                text);

        }

        private void DrawWeatherIcon()
        {
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            backgroundDrawList.PushClipRectFullScreen();
            try
            {
                if (!_weatherIconNode->AtkResNode.IsVisible) return;
                //Background of Weather Icon
                backgroundDrawList.AddImage(
                    _naviMapTextureD3D11ShaderResourceView
                    , _imGuiCompassData.WeatherIconBorderPMin
                    , _imGuiCompassData.WeatherIconBorderPMax
                    , new Vector2(0.08035714f, 0.8301887f)
                    , new Vector2(0.1607143f, 1));
                //Weather Icon
                var tex = _weatherIconNode->PartsList->Parts[0].UldAsset->AtkTexture.Resource->KernelTextureObject;
                backgroundDrawList.AddImage(
                    new IntPtr(tex->D3D11ShaderResourceView), _imGuiCompassData.WeatherIconPMin,
                    _imGuiCompassData.WeatherIconPMax);
                //Border around Weather Icon
                backgroundDrawList.AddImage(
                    _naviMapTextureD3D11ShaderResourceView
                    , _imGuiCompassData.WeatherIconBorderPMin
                    , _imGuiCompassData.WeatherIconBorderPMax
                    , new Vector2(0.1607143f, 0.8301887f)
                    , new Vector2(0.2410714f, 1));
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
                        var texFileNameStdString =
                            part.UldAsset->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        // NOTE (Chiv) We are in a try-catch, so we just throw if the read failed.
                        // Cannot act anyways if the texture path is butchered
                        var textureFileName = texFileNameStdString.ToString();
                        var _ = uint.TryParse(
                            textureFileName.Substring(textureFileName.LastIndexOfAny(new[] {'/', '\\'}) + 1, 6),
                            out var iconId);
                        //iconId = 0 (==> success == false as IconID will never be 0) Must have been 'NaviMap(_hr1)?\.tex' (and only that hopefully)
                        if (_config.FilteredIconIds.Contains(iconId)) continue;
                        
                        var textureIdPtr = new IntPtr(tex->D3D11ShaderResourceView);
                        //TODO DEBUG
//                        PluginLog.Debug($"TextureIdPtr is null? {textureIdPtr == IntPtr.Zero}");
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
                                var naviMapCutIconOffset = _imGuiCompassData.CompassUnit *
                                                           SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                               playerForward);
                                // We hope width == height
                                const int naviMapIconHalfWidth = 12;
                                var naviMapYOffset = 14 * _imGuiCompassData.Scale;
                                pMin = new Vector2(_imGuiCompassData.Centre.X - naviMapIconHalfWidth + naviMapCutIconOffset, _imGuiCompassData.Centre.Y - naviMapYOffset - naviMapIconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.Centre.X + naviMapCutIconOffset + naviMapIconHalfWidth, _imGuiCompassData.Centre.Y - naviMapYOffset + naviMapIconHalfWidth);
                                break;
                            case 1: // Rotation icons (except naviMap arrows) go here after setting up their UVs
                                // NOTE (Chiv) Rotations for icons on the map are mirrored from the
                                var rotationIconOffset = _imGuiCompassData.CompassUnit *
                                                         SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                             playerForward);
                                pMin = new Vector2(_imGuiCompassData.Centre.X - _imGuiCompassData.RotationIconHalfWidth + rotationIconOffset, _imGuiCompassData.Centre.Y - _imGuiCompassData.RotationIconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.Centre.X + rotationIconOffset + _imGuiCompassData.RotationIconHalfWidth, _imGuiCompassData.Centre.Y + _imGuiCompassData.RotationIconHalfWidth);
                                break;
                            case 060443: //Player Marker
                                if (!_config.ImGuiCompassEnableCenterMarker) continue;
                                drawList = ImGui.GetBackgroundDrawList();
                                pMin = new Vector2(_imGuiCompassData.Centre.X - _imGuiCompassData.HalfWidth40, _imGuiCompassData.Centre.Y + _config.ImGuiCompassCentreMarkerOffset * _imGuiCompassData.Scale - _imGuiCompassData.HalfWidth40);
                                pMax = new Vector2(_imGuiCompassData.Centre.X + _imGuiCompassData.HalfWidth40, _imGuiCompassData.Centre.Y + _config.ImGuiCompassCentreMarkerOffset * _imGuiCompassData.Scale + _imGuiCompassData.HalfWidth40);
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
                                        imgNode, distanceOffset, _imGuiCompassData.CompassUnit, _imGuiCompassData.HalfWidth40, _imGuiCompassData.Centre,
                                        maxDistance, _imGuiCompassData.MinScaleFactor);
                                if (inArea)
                                    //*((byte*) &tintColour + 3) = 0x33  == (0x33FFFFFF) & (tintColour)
                                    ImGui.GetBackgroundDrawList().AddRectFilled(_imGuiCompassData.BackgroundPMin
                                        , _imGuiCompassData.BackgroundPMax
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
                                        imgNode, distanceOffset, _imGuiCompassData.CompassUnit, _imGuiCompassData.HalfWidth40, _imGuiCompassData.Centre,
                                        maxDistance, _imGuiCompassData.MinScaleFactor);
                                break;
                            case 071003: // MSQ Ongoing Marker
                            case 071005: // MSQ Complete Marker
                            case 071013: // MSQ Ongoing Red Marker
                            case 071015: // MSQ Complete Red Marker
                            case 071023: // Quest Ongoing Marker
                            case 071033: // Quest Ongoing Red Marker
                            case 071153: // Quest Ongoing Red Marker 2
                            case 071025: // Quest Complete Marker
                            case 071035: // Quest Complete Red Marker
                            case 071155: // Quest Complete Red Marker
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
                            case 060463: // Mining Crop Icon 3
                            case 060464: // Mining Crop Icon 4
                            case 060432: // Botanic Crop Icon 
                            case 060433: // Botanic Crop Icon 2 
                            case 060461: // Botanic Crop Icon 3
                            case 060462: // Botanic Crop Icon 4
                            case 060455: // Fishing Icon 
                            case 060465: // Fishing Icon 2 
                            case 060466: // Fishing Icon 3
                            case 060474: // Waymark A
                            case 060475: // Waymark B
                            case 060476: // Waymark C
                            case 060936: // Waymark D
                            case 060931: // Waymark 1
                            case 060932: // Waymark 2
                            case 060933: // Waymark 3
                            case 063904: // Waymark 4
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
                                var iconOffset = _imGuiCompassData.CompassUnit * iconAngle;
                                var iconHalfWidth = _imGuiCompassData.HalfWidth40 * distanceScaleFactor;
                                pMin = new Vector2(_imGuiCompassData.Centre.X - iconHalfWidth + iconOffset, _imGuiCompassData.Centre.Y - iconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.Centre.X + iconOffset + iconHalfWidth, _imGuiCompassData.Centre.Y + iconHalfWidth);
                                break;
                        }
                        //PluginLog.Debug($"ID: {iconId}; Tintcolor: {tintColour:x8}");
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
        private static unsafe void ImageRotated(IntPtr texId, Vector2 center, Vector2 size, float angle, Vector2 uv, Vector2 uv1, ImDrawListPtr? drawList = null)
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
            var eastOffset = _imGuiCompassData.CompassUnit * SignedAngle(east, playerForward);
            var pMinY = _imGuiCompassData.Centre.Y - _imGuiCompassData.HalfWidth40 + _config.ImGuiCompassCardinalsOffset;
            var pMaxY = _imGuiCompassData.Centre.Y + _imGuiCompassData.HalfWidth40 + _config.ImGuiCompassCardinalsOffset;
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.Centre.X - _imGuiCompassData.HalfWidth28 + eastOffset, pMinY)
                , new Vector2(_imGuiCompassData.Centre.X + eastOffset + _imGuiCompassData.HalfWidth28, pMaxY)
                , new Vector2(0.5446429f, 0.8301887f)
                , new Vector2(0.5892857f, 0.9811321f)
            );
            var southOffset = _imGuiCompassData.CompassUnit * SignedAngle(south, playerForward);
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.Centre.X - _imGuiCompassData.HalfWidth28 + southOffset, pMinY)
                , new Vector2(_imGuiCompassData.Centre.X + southOffset + _imGuiCompassData.HalfWidth28, pMaxY)
                , new Vector2(0.5892857f, 0.8301887f)
                , new Vector2(0.6339286f, 0.9811321f)
            );
            var westOffset = _imGuiCompassData.CompassUnit * SignedAngle(west, playerForward);
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.Centre.X - _imGuiCompassData.HalfWidth40 + westOffset, pMinY)
                , new Vector2(_imGuiCompassData.Centre.X + westOffset + _imGuiCompassData.HalfWidth40, pMaxY)
                , new Vector2(0.4732143f, 0.8301887f)
                , new Vector2(0.5446429f, 0.9811321f)
            );
            var northOffset = _imGuiCompassData.CompassUnit * SignedAngle(north, playerForward);
            backgroundDrawList.AddImage(
                _naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.Centre.X - _imGuiCompassData.HalfWidth40 + northOffset, pMinY)
                , new Vector2(_imGuiCompassData.Centre.X + northOffset + _imGuiCompassData.HalfWidth40, pMaxY)
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
                backgroundDrawList.AddRectFilled(_imGuiCompassData.BackgroundPMin
                    , _imGuiCompassData.BackgroundPMax
                    , _imGuiCompassData.BackgroundColourUInt32
                    , _config.ImGuiCompassBackgroundRounding
                );
            if (_config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Border or ImGuiCompassBackgroundStyle.FilledAndBorder)
                backgroundDrawList.AddRect(_imGuiCompassData.BackgroundPMin - Vector2.One
                    , _imGuiCompassData.BackgroundPMax + Vector2.One
                    , _imGuiCompassData.BackgroundBorderColourUInt32
                    , _config.ImGuiCompassBackgroundRounding
                    , ImDrawFlags.RoundCornersAll
                    , _config.ImGuiBackgroundBorderThickness
                );
            if (_config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Line)
                backgroundDrawList.AddLine(_imGuiCompassData.BackgroundLinePMin
                    , _imGuiCompassData.BackgroundLinePMax
                    , _imGuiCompassData.BackgroundLineColourUInt32
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
                var unitBase = _gameGui.GetAddonByName(uiIdentifier, 1);
                if (unitBase == IntPtr.Zero) continue;
                _shouldHideCompassIteration |= ((AtkUnitBase*) unitBase)->IsVisible;
            }
        }
    }
}
