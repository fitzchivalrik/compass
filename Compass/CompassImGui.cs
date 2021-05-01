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
    public partial class Compass
    {
        private ImGuiCompassData _imGuiCompassData =  new() 
        {
            PlayerPosition = new Vector2(ImGuiCompassData.NaviMapPlayerX, ImGuiCompassData.NaviMapPlayerY),
            ImGuiCompassCentre = new Vector2(835 + 125f, 515 + 25f),
            ImGuiCompassBackgroundPMin = Vector2.Zero,
            ImGuiCompassBackgroundPMax = Vector2.Zero,
            ImGuiCompassBackgroundLinePMin = Vector2.Zero,
            ImGuiCompassBackgroundLinePMax = Vector2.Zero,
            ImGuiCompassHalfWidth = 125f,
            ImGuiCompassHalfHeight = 125f,
            ImGuicompassScale = 1f,
            CompassHeightScale = 1f,
            DistanceScaleFactorForRotationIcons = 1f,
            HalfWidth40 = 20f,
            HalfWidth28 = 14f,
            ImGuiCompassUnit = 0f,
            RotationIconHalfWidth = 12f,
            ImGuiBackgroundColourUInt32 = ImGuiCompassData.WhiteColor,
            ImGuiBackgroundBorderColourUInt32 = ImGuiCompassData.WhiteColor,
            ImGuiBackgroundLineColourUInt32 = ImGuiCompassData.WhiteColor
        };
        
        private void UpdateCompassVariables()
        {
            _imGuiCompassData.ImGuicompassScale = _config.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
            _imGuiCompassData.CompassHeightScale = ImGui.GetIO().FontGlobalScale;

            _imGuiCompassData.DistanceScaleFactorForRotationIcons = _imGuiCompassData.ImGuicompassScale * 0.7f;
            _imGuiCompassData.RotationIconHalfWidth = 16f * _imGuiCompassData.DistanceScaleFactorForRotationIcons;

            _imGuiCompassData.ImGuiCompassHalfWidth = _config.ImGuiCompassWidth * 0.5f;
            _imGuiCompassData.ImGuiCompassHalfHeight = ImGuiCompassData.ImGuiCompassHeight * 0.5f * _imGuiCompassData.CompassHeightScale;

            _imGuiCompassData.ImGuiCompassUnit = _config.ImGuiCompassWidth / (2f*(float)Math.PI);
            // TODO Main viewport position offset?
            _imGuiCompassData.ImGuiCompassCentre =
                new Vector2(_config.ImGuiCompassPosition.X + _imGuiCompassData.ImGuiCompassHalfWidth,
                    _config.ImGuiCompassPosition.Y + _imGuiCompassData.ImGuiCompassHalfHeight);

            _imGuiCompassData.ImGuiCompassBackgroundPMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - 5 - _imGuiCompassData.ImGuiCompassHalfWidth, _imGuiCompassData.ImGuiCompassCentre.Y - _imGuiCompassData.ImGuiCompassHalfHeight * 0.5f - 2);
            _imGuiCompassData.ImGuiCompassBackgroundPMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + 5 + _imGuiCompassData.ImGuiCompassHalfWidth, _imGuiCompassData.ImGuiCompassCentre.Y + _imGuiCompassData.ImGuiCompassHalfHeight * 0.5f + 2);
            _imGuiCompassData.ImGuiCompassBackgroundLinePMin = new Vector2(_imGuiCompassData.ImGuiCompassBackgroundPMin.X, _config.ImGuiCompassBackgroundLineOffset + _imGuiCompassData.ImGuiCompassBackgroundPMin.Y + _imGuiCompassData.ImGuiCompassHalfHeight);
            _imGuiCompassData.ImGuiCompassBackgroundLinePMax = new Vector2(_imGuiCompassData.ImGuiCompassBackgroundPMax.X, _config.ImGuiCompassBackgroundLineOffset + _imGuiCompassData.ImGuiCompassBackgroundPMin.Y + _imGuiCompassData.ImGuiCompassHalfHeight);

            _imGuiCompassData.HalfWidth40 = 20 * _imGuiCompassData.ImGuicompassScale;
            _imGuiCompassData.HalfWidth28 = 14 * _imGuiCompassData.ImGuicompassScale;

            _imGuiCompassData.ImGuiBackgroundColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundColour);
            _imGuiCompassData.ImGuiBackgroundBorderColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundBorderColour);
            _imGuiCompassData.ImGuiBackgroundLineColourUInt32 = ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundLineColour);
            
            _uiIdentifiers = _config.ShouldHideOnUiObject
                .Where(it => it.disable)
                .SelectMany(it => it.getUiObjectIdentifier)
                .ToArray();
            _currentUiObjectIndex = 0;
        }
        
        
        // TODO Cut this s@>!? in smaller methods
        private unsafe void BuildImGuiCompassNavi()
        {
            if (!_config.ImGuiCompassEnable) return;
            if (_config.HideInCombat && _pluginInterface.ClientState.Condition[ConditionFlag.InCombat]) return;
            UpdateHideCompass();
            if (_shouldHideCompass) return;
            //var naviMapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            //if (naviMapPtr == IntPtr.Zero) return;
            //var naviMap = (AtkUnitBase*) naviMapPtr;
            //var _naviMap = (AtkUnitBase*)_pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            //  TODO YOLO, lets just assume naviMap never invalidates
            //NOTE (chiv) 3 means fully loaded
            if (_naviMap->ULDData.LoadedState != 3) return;
            if (!_naviMap->IsVisible) return;
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
                                           | ImGuiWindowFlags.NoMove
                                           | ImGuiWindowFlags.NoMouseInputs
                                           | ImGuiWindowFlags.NoFocusOnAppearing
                                           | ImGuiWindowFlags.NoBackground
                                           | ImGuiWindowFlags.NoNav
                                           | ImGuiWindowFlags.NoInputs
                                           | ImGuiWindowFlags.NoCollapse;
            //TODO ForceWindow code back again + test
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Vector2.Zero, ImGuiCond.Always);
            if (!ImGui.Begin("###ImGuiCompassWindow", flags)
            )
            {
                ImGui.End();
                return;
            }
            var drawlist = ImGui.GetWindowDrawList();
            drawlist.PushClipRectFullScreen();
            // 0 == Facing North, -PI/2 facing east, PI/2 facing west.
            //var cameraRotationInRadian = *(float*) (_maybeCameraStruct + 0x130);
            //var _miniMapIconsRootComponentNode = (AtkComponentNode*)_naviMap->ULDData.NodeList[2];
            // Minimap rotation thingy is even already flipped!
            // And apparently even accessible & updated if _NaviMap is disabled
            // => This leads to jerky behaviour though
            var cameraRotationInRadian = *(float*)((long)_naviMap + 0x254) * Deg2Rad;
            
            var cosPlayer = (float) Math.Cos(cameraRotationInRadian);
            var sinPlayer = (float) Math.Sin(cameraRotationInRadian);
            // NOTE (Chiv) Interpret game's camera rotation as
            // 0 => (0,1) (North), PI/2 => (-1,0) (West)  in default coordinate system
            // Games Map coordinate system origin is upper left, with positive Y grow
            var playerForward = new Vector2(-sinPlayer, cosPlayer);
            
            //First, the background
            DrawImGuiCompassBackground();
            // Second, we position our Cardinals
            var westCardinalAtkImageNode = (AtkImageNode*) _naviMap->ULDData.NodeList[11];
            // TODO (Chiv) Cache on TerritoryChange/Initialisation?
            var naviMapTextureD3D11ShaderResourceView = new IntPtr(
                westCardinalAtkImageNode->PartsList->Parts[0]
                    .UldAsset->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
            );
            DrawCardinals(playerForward, naviMapTextureD3D11ShaderResourceView);
            DrawCompassIcons(playerForward);
            drawlist.PopClipRect();
            ImGui.End();
        }
        
        private unsafe void DrawCompassIcons(Vector2 playerForward)
        {
            try
            {
                var drawList = ImGui.GetWindowDrawList();
                // Then, we do the dance through all relevant nodes on _NaviMap
                // I imagine this throws sometimes because of racing conditions -> We try to access an already freed texture e.g.
                // So we just ignore those small exceptions, it works a few frames later anyways
                var mapScale =
                    //miniMapIconsRootComponentNode->Component->ULDData.NodeList[1]->ScaleX; //maxZoom level == 2
                    *(float*) ((long) _naviMap + 0x24C);
                for (var i = 4; i < _miniMapIconsRootComponentNode->Component->ULDData.NodeListCount; i++)
                {
                    var mapIconComponentNode =
                        (AtkComponentNode*) _miniMapIconsRootComponentNode->Component->ULDData.NodeList[i];
                    if (!mapIconComponentNode->AtkResNode.IsVisible) continue;
                    for (var j = 2; j < mapIconComponentNode->Component->ULDData.NodeListCount; j++)
                    {
                        // NOTE (Chiv) Invariant: From 2 onward, only ImageNodes
                        var imgNode = (AtkImageNode*) mapIconComponentNode->Component->ULDData.NodeList[j];
                        // TODO
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
                        //var success = uint.TryParse(textureFileName.Substring(textureFileName.LastIndexOf('/')+1, 6), out var iconId);
                        var _ = uint.TryParse(
                            textureFileName.Substring(textureFileName.LastIndexOfAny(new[] {'/', '\\'}) + 1, 6),
                            out var iconId);
                        //iconId = 0 (==> success == false as IconID will never be 0) Must have been NaviMap (and only that hopefully)
                        var textureIdPtr = new IntPtr(tex->D3D11ShaderResourceView);
                        Vector2 pMin;
                        Vector2 pMax;
                        var uv = Vector2.Zero;
                        var uv1 = Vector2.One;
                        var tintColour = ImGuiCompassData.WhiteColor;
                        var rotate = false;
                        switch (iconId)
                        {
                            case 0 when imgNode->PartId == 21: //Glowy thingy
                                rotate = true; // TODO I guess better to just duplicate then to introduce branching just for that
                                uv = new Vector2( (float) part.U / 448, (float) part.V / 212);
                                uv1 = new Vector2( (float) (part.U + 40) / 448, (float) (part.V + 40) / 212);
                                if (mapIconComponentNode->AtkResNode.Rotation == 0)
                                    goto default;
                                goto case 1;
                            case 0: //Arrows to quests and fates
                                // NOTE (Chiv) We assume part.Width == part.Height == 24
                                // NOTE (Chiv) We assume tex.Width == 448 && tex.Height == 212
                                //TODO (Chiv) Will break on glowy under thingy, need to test if 'if' or 'read' is slower, I assume if
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
                                // TODO (Chiv) Glowing thingy is not
                                var naviMapCutIconOffset = _imGuiCompassData.ImGuiCompassUnit *
                                                           SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                               playerForward);
                                // We hope width == height
                                const int naviMapIconHalfWidth = 12;
                                var naviMapYOffset = 14 * _imGuiCompassData.ImGuicompassScale;
                                pMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - naviMapIconHalfWidth + naviMapCutIconOffset, _imGuiCompassData.ImGuiCompassCentre.Y - naviMapYOffset - naviMapIconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + naviMapCutIconOffset + naviMapIconHalfWidth, _imGuiCompassData.ImGuiCompassCentre.Y - naviMapYOffset + naviMapIconHalfWidth);
                                break;
                            case 1: // Rotation icons (except naviMap arrows) go here after setting up their UVs
                                // NOTE (Chiv) Rotations for icons on the map are mirrowed from the
                                var rotationIconOffset = _imGuiCompassData.ImGuiCompassUnit *
                                                         SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                             playerForward);
                                pMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.RotationIconHalfWidth + rotationIconOffset, _imGuiCompassData.ImGuiCompassCentre.Y - _imGuiCompassData.RotationIconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + rotationIconOffset + _imGuiCompassData.RotationIconHalfWidth, _imGuiCompassData.ImGuiCompassCentre.Y + _imGuiCompassData.RotationIconHalfWidth);
                                break;
                            case 060443: //Player Marker
                                if (!_config.ImGuiCompassEnableCenterMarker) continue;
                                drawList = ImGui.GetBackgroundDrawList();
                                //SimpleLog.Log("Player marker");
                                pMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth40, _imGuiCompassData.ImGuiCompassCentre.Y + _config.ImGuiCompassCentreMarkerOffset * _imGuiCompassData.ImGuicompassScale - _imGuiCompassData.HalfWidth40);
                                pMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + _imGuiCompassData.HalfWidth40, _imGuiCompassData.ImGuiCompassCentre.Y + _config.ImGuiCompassCentreMarkerOffset * _imGuiCompassData.ImGuicompassScale + _imGuiCompassData.HalfWidth40);
                                uv1 = _config.ImGuiCompassFlipCentreMarker ? new Vector2(1, -1) : Vector2.One;
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
                                        imgNode, mapScale, _imGuiCompassData.ImGuiCompassUnit, _imGuiCompassData.HalfWidth40, _imGuiCompassData.ImGuiCompassCentre);
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
                                        imgNode, mapScale, _imGuiCompassData.ImGuiCompassUnit, _imGuiCompassData.HalfWidth40, _imGuiCompassData.ImGuiCompassCentre);
                                break;
                            case 071023: // Quest Ongoing Marker
                            case 071025: // Quest Complete Marker
                            case 071063: // BookQuest Ongoing Marker
                            case 071065: // BookQuest Complete Marker
                            case 071083: // LeveQuest Ongoing Marker
                            case 071085: // LeveQuest Complete Marker
                            case 071143: // BlueQuest Ongoing Marker
                            case 071145: // BlueQuest Complete Marker
                            case 071146: // Weird Blueish Round Exclamation Mark TODO (Chiv): there are a lot more of those
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
                            case 060457: // Area Transition Bullet Thingy
                            default:
                                // NOTE (Chiv) Remember, Y needs to be flipped to transform to default coordinate system
                                var (distanceScaleFactor, iconAngle, _) = CalculateDrawVariables(_imGuiCompassData.PlayerPosition,
                                    new Vector2(
                                        mapIconComponentNode->AtkResNode.X,
                                        -mapIconComponentNode->AtkResNode.Y
                                    ),
                                    playerForward,
                                    mapScale
                                );
                                // NOTE (Chiv) We assume part.Width == part.Height == 32
                                var iconOffset = _imGuiCompassData.ImGuiCompassUnit * iconAngle;
                                var iconHalfWidth = _imGuiCompassData.HalfWidth40 * distanceScaleFactor;
                                pMin = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - iconHalfWidth + iconOffset, _imGuiCompassData.ImGuiCompassCentre.Y - iconHalfWidth);
                                pMax = new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + iconOffset + iconHalfWidth, _imGuiCompassData.ImGuiCompassCentre.Y + iconHalfWidth);
                                if (mapIconComponentNode->AtkResNode.NodeID == 30107)
                                {
                                 //   SimpleLog.Log($"{imgNode->AtkResNode.NodeID}:{iconId} {pMin} {pMax} ");
                                }
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
            var pos = new[]
            {
                /*
                center + ImRotate(new Vector2(0, size.Y), cos_a, sin_a),
                center + ImRotate(new Vector2(+size.X, size.Y), cos_a, sin_a),
                center + ImRotate(new Vector2(+size.X, 0), cos_a, sin_a),
                center + ImRotate(new Vector2(0, 0), cos_a, sin_a)
                */
                center + Rotate(new Vector2(-size.X * 0.5f, -size.Y * 0.5f), cosA, sinA),
                center + Rotate(new Vector2(+size.X * 0.5f, -size.Y * 0.5f), cosA, sinA),
                center + Rotate(new Vector2(+size.X * 0.5f, +size.Y * 0.5f), cosA, sinA),
                center + Rotate(new Vector2(-size.X * 0.5f, +size.Y * 0.5f), cosA, sinA)
                
            };
            var uvs = new[] 
            { 
                new Vector2(uv.X, uv.Y),
                new Vector2(uv1.X, uv.Y),
                new Vector2(uv1.X, uv1.Y),
                new Vector2(uv.X, uv1.Y)
            };
            drawList.Value.AddImageQuad(texId, pos[0], pos[1], pos[2], pos[3], uvs[0], uvs[1], uvs[2], uvs[3]);
        }

        // TODO (Chiv) Remove the duplicated code between the two data sources
         private unsafe void BuildImGuiCompassArea()
        {
            if (!_config.ImGuiCompassEnable) return;
            if (_config.HideInCombat && _pluginInterface.ClientState.Condition[ConditionFlag.InCombat]) return;
            UpdateHideCompass();
            if (_shouldHideCompass) return;
            var areaMapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("AreaMap", 1);
            if (areaMapPtr == IntPtr.Zero) return;
            var areaMap = (AtkUnitBase*) areaMapPtr;
            //NOTE (chiv) 3 means fully loaded
            if (areaMap->ULDData.LoadedState != 3) return;
            if (!areaMap->IsVisible) return;
            var scale = _config.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
            var heightScale = ImGui.GetIO().FontGlobalScale;
            const float windowHeight = 50f;
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
                                           | ImGuiWindowFlags.NoMove
                                           | ImGuiWindowFlags.NoMouseInputs
                                           | ImGuiWindowFlags.NoFocusOnAppearing
                                           | ImGuiWindowFlags.NoBackground
                                           | ImGuiWindowFlags.NoNav
                                           | ImGuiWindowFlags.NoInputs
                                           | ImGuiWindowFlags.NoCollapse;
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowSizeConstraints(
                new Vector2(250f, (windowHeight + 20) * heightScale),
                new Vector2(int.MaxValue, (windowHeight + 20) * heightScale));
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Vector2.Zero, ImGuiCond.FirstUseEver);
            if (!ImGui.Begin("###ImGuiCompassWindow"
                , _buildingConfigUi
                    ? ImGuiWindowFlags.NoCollapse
                      | ImGuiWindowFlags.NoTitleBar
                      | ImGuiWindowFlags.NoFocusOnAppearing
                      | ImGuiWindowFlags.NoScrollbar
                      | ImGuiWindowFlags.NoBackground
                    : flags)
            )
            {
                ImGui.End();
                return;
            }
            const uint whiteColor = 0xFFFFFFFF;
            // 0 == Facing North, -PI/2 facing east, PI/2 facing west.
            var cameraRotationInRadian = *(float*) (_maybeCameraStruct + 0x130);
            //var cameraRotationInRadian = *(float*)(naviMapPtr + 0x254) * Deg2Rad;
            var areaMapIconsRootComponentNode = (AtkComponentNode*) areaMap->ULDData.NodeList[3];
            if (areaMapIconsRootComponentNode->Component->ULDData.NodeListCount != 265)
            {
                ImGui.End();
                return;
            }
            var distanceScaleFactorForRotationIcons = scale * 0.7f;
            var cosPlayer = (float) Math.Cos(cameraRotationInRadian);
            var sinPlayer = (float) Math.Sin(cameraRotationInRadian);
            // NOTE (Chiv) Interpret game's camera rotation as
            // 0 => (0,1) (North), PI/2 => (-1,0) (West)  in default coordinate system
            // Games Map coordinate system origin is upper left, with positive Y grow
            var playerForward = new Vector2(-sinPlayer, cosPlayer);
            var zeroVec = Vector2.Zero;
            var oneVec = Vector2.One;
            var widthOfCompass = ImGui.GetWindowContentRegionWidth();
            var halfWidthOfCompass = widthOfCompass * 0.5f;
            var halfHeightOfCompass = windowHeight * 0.5f * heightScale;
            var compassUnit = widthOfCompass / (2f*(float)Math.PI);

            var drawList = ImGui.GetWindowDrawList();
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            var cursorPosition = ImGui.GetCursorScreenPos();
            var compassCentre =
                new Vector2(cursorPosition.X + halfWidthOfCompass, cursorPosition.Y + halfHeightOfCompass);
            var halfWidth32 = 16 * scale;
            var backgroundPMin = new Vector2(compassCentre.X - 5 - halfWidthOfCompass,
                compassCentre.Y - halfHeightOfCompass * 0.5f - 2);
            var backgroundPMax = new Vector2(compassCentre.X + 5 + halfWidthOfCompass,
                compassCentre.Y + halfHeightOfCompass * 0.5f + 2);
            //First, the background
            DrawImGuiCompassBackground();
            // Second, we position our Cardinals
            var rotationTriangleImageNode = (AtkImageNode*) areaMapIconsRootComponentNode->Component->ULDData.NodeList[5];
            // TODO (Chiv) Cache on TerritoryChange/Initialisation?
            var naviMapTextureD3D11ShaderResourceView = new IntPtr(
                rotationTriangleImageNode->PartsList->Parts[0]
                    .UldAsset->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
            );
            DrawCardinals(playerForward, naviMapTextureD3D11ShaderResourceView);
            try
            {
                // Then, we do the dance through all relevant nodes on _NaviMap
                // I imagine this throws sometimes because of racing conditions -> We try to access an already freed texture e.g.
                // So we just ignore those small exceptions, it works a few frames later anyways
                var mapSlider =
                    (AtkComponentNode*)areaMap->ULDData.NodeList[32]; //maxZoom level == 2
                // NOTE (Chiv) Slider position value is at this address
                var mapScale = *(byte*) ((nint)(mapSlider->Component)+0xF0) + 1;
                //SimpleLog.Log($"MapScale {mapScale}");
                // NOTE (Chiv) We go down because we assume
                // (a) The first visible node will always be the player marker and give us the player positoin
                // (b) everything higher than 231 is region text node which we ignore (for now)
                //231 && 6
                for (var i = 231; i > 6; i--)
                {
                    var mapIconComponentNode =
                        (AtkComponentNode*) areaMapIconsRootComponentNode->Component->ULDData.NodeList[i];
                    if (!mapIconComponentNode->AtkResNode.IsVisible) continue;
                    //SimpleLog.Log($"Player pos {playerPos}");
                    for (var j = 3; j < 6; j++)
                    {
                        var imgNode = (AtkImageNode*) mapIconComponentNode->Component->ULDData.NodeList[j];
                        if (imgNode->AtkResNode.Type != NodeType.Image)
                        {
                            continue;
                        }
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
                        //var success = uint.TryParse(textureFileName.Substring(textureFileName.LastIndexOf('/')+1, 6), out var iconId);
                        var _ = uint.TryParse(textureFileName.Substring(textureFileName.LastIndexOf('/')+1, 6),
                            out var iconId);
                        //iconId = 0 (==> success == false as IconID will never be 0) Must have been NaviMap (and only that hopefully)
                        var textureIdPtr = new IntPtr(tex->D3D11ShaderResourceView);
                        Vector2 pMin;
                        Vector2 pMax;
                        var uv = zeroVec;
                        var uv1 = oneVec;
                        var tintColour = whiteColor;

                        const float maxDistance = 62f;
                        const float distanceOffset = 2f;
                        const float lowestScaleFactor = 0.5f;
                        switch (iconId)
                        {
                            case 0: //Arrows to quests and fates, glowy thingy
                                continue; //Nothing here in AreaMap we wanna draw, just glowy thing. For now.
                                // NOTE (Chiv) We assume part.Width == part.Height == 24
                                // NOTE (Chiv) We assume tex.Width == 448 && tex.Height == 212
                                //TODO (Chiv) Will break on glowy under thingy, need to test if 'if' or 'read' is slower, I assume if
                                //var u = (float) part.U / 448; // = (float) part.U / tex->Width;
                                //var v = (float) part.V / 212; // = (float) part.V / tex->Height;
                                //var u1 = (float) (part.U + 24) / 448; // = (float) (part.U + part.Width) / tex->Width;
                                //var v1 = (float) (part.V + 24) / 212; // = (float) (part.V + part.Height) / tex->Height;
                                var u = (float) part.U / tex->Width;
                                var v = (float) part.V / tex->Height; 
                                var u1 = (float) (part.U + part.Width) / tex->Width; 
                                var v1 = (float) (part.V + part.Height) / tex->Height;
                                
                                uv = new Vector2(u, v);
                                uv1 = new Vector2(u1, v1);
                                // Arrows and such are always rotation based, we draw them slightly on top
                                // TODO (Chiv) Glowing thingy is not
                                var naviMapCutIconOffset = compassUnit *
                                                           SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                               playerForward);
                                // We hope width == height
                                const int naviMapIconHalfWidth = 12;
                                var naviMapYOffset = 12 * scale;
                                pMin = new Vector2(compassCentre.X - naviMapIconHalfWidth + naviMapCutIconOffset,
                                    compassCentre.Y - naviMapYOffset - naviMapIconHalfWidth);
                                pMax = new Vector2(
                                    compassCentre.X + naviMapCutIconOffset + naviMapIconHalfWidth,
                                    compassCentre.Y - naviMapYOffset + naviMapIconHalfWidth);
                                //SimpleLog.Log($"{i} {j}");
                                break;
                            case 1: // Rotation icons (except naviMap arrows) go here after setting up their UVs
                                // NOTE (Chiv) Rotations for icons on the map are mirrowed from the
                                var rotationIconOffset = compassUnit *
                                                       SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                           playerForward);
                                // We hope width == height
                                var rotationIconHalfWidth = 12f * distanceScaleFactorForRotationIcons;
                                pMin = new Vector2(compassCentre.X - rotationIconHalfWidth + rotationIconOffset,
                                    compassCentre.Y - rotationIconHalfWidth);
                                pMax = new Vector2(
                                    compassCentre.X + rotationIconOffset + rotationIconHalfWidth,
                                    compassCentre.Y + rotationIconHalfWidth);
                                break;
                            case 060443: //Player Marker
                                _lastKnownPlayerPos = new Vector2(mapIconComponentNode->AtkResNode.X,
                                    -mapIconComponentNode->AtkResNode.Y);
                                if (!_config.ImGuiCompassEnableCenterMarker) continue;
                                drawList = backgroundDrawList;
                                pMin = new Vector2(compassCentre.X - halfWidth32,
                                    compassCentre.Y + _config.ImGuiCompassCentreMarkerOffset * scale -
                                    halfWidth32);
                                pMax = new Vector2(compassCentre.X + halfWidth32,
                                    compassCentre.Y + _config.ImGuiCompassCentreMarkerOffset * scale +
                                    halfWidth32);
                                uv1 = _config.ImGuiCompassFlipCentreMarker ? new Vector2(1, -1) : oneVec;
                                break;
                            case 060495: // Small Area Circle
                            case 060496: // Big Area Circle
                            case 060497: // Another Circle
                            case 060498: // One More Circle
                                bool inArea;
                                (pMin, pMax, tintColour, inArea)
                                    = CalculateAreaCirlceVariables(_lastKnownPlayerPos, playerForward, mapIconComponentNode,
                                        imgNode, mapScale, compassUnit, halfWidth32, compassCentre,
                                        maxDistance,distanceOffset, lowestScaleFactor);
                                if (inArea)
                                    //*((byte*) &tintColour + 3) = 0x33  == (0x33FFFFFF) & (tintColour)
                                    backgroundDrawList.AddRectFilled(
                                        backgroundPMin
                                        , backgroundPMax
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
                                    = CalculateAreaCirlceVariables(_lastKnownPlayerPos, playerForward, mapIconComponentNode,
                                        imgNode, mapScale, compassUnit, halfWidth32, compassCentre, maxDistance, distanceOffset, lowestScaleFactor);
                                break;
                            case 071023: // Quest Ongoing Marker
                            case 071025: // Quest Complete Marker
                            case 071063: // BookQuest Ongoing Marker
                            case 071065: // BookQuest Complete Marker
                            case 071083: // LeveQuest Ongoing Marker
                            case 071085: // LeveQuest Complete Marker
                            case 071143: // BlueQuest Ongoing Marker
                            case 071145: // BlueQuest Complete Marker
                            case 060954: // Arrow up for quests
                            case 060955: // Arrow down for quests
                            case 060561: // Red Flag (Custom marker)
                                if (mapIconComponentNode->AtkResNode.Rotation == 0)
                                {
                                    var (ongoingScaleFactor, ongoingIconAngle, _) = CalculateDrawVariables(
                                        _lastKnownPlayerPos,
                                        new Vector2(
                                            mapIconComponentNode->AtkResNode.X,
                                            -mapIconComponentNode->AtkResNode.Y
                                        ),
                                        playerForward,
                                        mapScale,
                                        maxDistance,
                                        distanceOffset,
                                        lowestScaleFactor
                                    );
                                    //SimpleLog.Log($"S{iconId} ScaleFactor:{ongoingScaleFactor} ");
                                    //SimpleLog.Log($"PlayerPos {playerPos}, iconPos {new Vector2(mapIconComponentNode->AtkResNode.X, -mapIconComponentNode->AtkResNode.Y)}");
                                    // NOTE (Chiv) We assume part.Width == part.Height == 32
                                    var ongoingIconOffset = compassUnit * ongoingIconAngle;
                                    var ongoingIconHalfWidth = halfWidth32 * ongoingScaleFactor;
                                    pMin = new Vector2(compassCentre.X - ongoingIconHalfWidth + ongoingIconOffset,
                                        compassCentre.Y - ongoingIconHalfWidth);
                                    pMax = new Vector2(
                                        compassCentre.X + ongoingIconOffset + ongoingIconHalfWidth,
                                        compassCentre.Y + ongoingIconHalfWidth);
                                    // => The current quest marker is inside the mask and should be
                                    // treated as a map point
                                    break;
                                    goto default;
                                }
                                    
                                // => The current quest marker is outside the mask and should be
                                // treated as a rotation
                                goto case 1; //No UV setup needed for quest markers
                            case 060442: // Map point for area description, its just flavour
                                continue;
                            case 060457: // Area Transition Bullet Thingy
                            default:
                                // NOTE (Chiv) Remember, Y needs to be flipped to transform to default coordinate system
                                var (distanceScaleFactor, iconAngle, _) = CalculateDrawVariables(
                                    _lastKnownPlayerPos,
                                    new Vector2(
                                        mapIconComponentNode->AtkResNode.X,
                                        -mapIconComponentNode->AtkResNode.Y
                                    ),
                                    playerForward,
                                    mapScale,
                                    maxDistance,
                                    distanceOffset,
                                    0f
                                );
                                //SimpleLog.Log($"Distance to {iconId}:{d} ");
                                //SimpleLog.Log($"PlayerPos {playerPos}, iconPos {new Vector2(mapIconComponentNode->AtkResNode.X, -mapIconComponentNode->AtkResNode.Y)}");
                                // NOTE (Chiv) We assume part.Width == part.Height == 32
                                var iconOffset = compassUnit * iconAngle;
                                var iconHalfWidth = halfWidth32 * distanceScaleFactor;
                                pMin = new Vector2(compassCentre.X - iconHalfWidth + iconOffset,
                                    compassCentre.Y - iconHalfWidth);
                                pMax = new Vector2(
                                    compassCentre.X + iconOffset + iconHalfWidth,
                                    compassCentre.Y + iconHalfWidth);
                                break;
                        }

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
            ImGui.End();
        }
        
        
        private void DrawCardinals(Vector2 playerForward,
            IntPtr naviMapTextureD3D11ShaderResourceView)
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
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth28 + eastOffset, pMinY) // Width = 20
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + eastOffset + _imGuiCompassData.HalfWidth28, pMaxY)
                , new Vector2(0.5446429f, 0.8301887f)
                , new Vector2(0.5892857f, 0.9811321f)
            );
            var southOffset = _imGuiCompassData.ImGuiCompassUnit * SignedAngle(south, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth28 + southOffset, pMinY)
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + southOffset + _imGuiCompassData.HalfWidth28, pMaxY)
                , new Vector2(0.5892857f, 0.8301887f)
                , new Vector2(0.6339286f, 0.9811321f)
            );
            var westOffset = _imGuiCompassData.ImGuiCompassUnit * SignedAngle(west, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth40 + westOffset, pMinY)
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + westOffset + _imGuiCompassData.HalfWidth40, pMaxY)
                , new Vector2(0.4732143f, 0.8301887f)
                , new Vector2(0.5446429f, 0.9811321f)
            );
            var northOffset = _imGuiCompassData.ImGuiCompassUnit * SignedAngle(north, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X - _imGuiCompassData.HalfWidth40 + northOffset, pMinY)
                , new Vector2(_imGuiCompassData.ImGuiCompassCentre.X + northOffset + _imGuiCompassData.HalfWidth40, pMaxY)
                , new Vector2(0.4017857f, 0.8301887f)
                , new Vector2(0.4732143f, 0.9811321f)
                , 0xFF0064B0 //ABGR ImGui.ColorConvertFloat4ToU32(new Vector4(176f / 255f, 100f / 255f, 0f, 1)) // TODO Static const
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
        
        private unsafe void UpdateHideCompass()
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