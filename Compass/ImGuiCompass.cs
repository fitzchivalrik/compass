using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState;
using Dalamud.Game.Internal;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin;

namespace Compass
{
    public class ImGuiCompass
    {
        private const int UiObjectsCheckPerFrame = 8;
        private readonly string[] _uiIdentifiers;
        private int _currentUiObjectIndex;
        private bool _shouldHideCompassIteration;
        private bool _shouldHideCompass;

        
        public static ImGuiCompass CreateImGuiCompass(Configuration config)
        {
            var uiIdentifiers = config.ShouldHideOnUiObject
                .Where(it => it.disable)
                .SelectMany(it => it.getUiObjectIdentifier)
                .ToArray();
            return new ImGuiCompass(uiIdentifiers);
        }

        private ImGuiCompass(string[] uiIdentifiers)
        {
            _uiIdentifiers = uiIdentifiers;
        }

        
        // TODO Cut this s@>!? in smaller methods
        public unsafe void BuildImGuiCompass(Configuration config, Framework framework,
            Condition condition, bool configureable, nint maybeCameraStruct)
        {
            UpdateHideCompass(config, framework, condition);
            if (_shouldHideCompass) return;
            if (!config.ImGuiCompassEnable) return;
            var naviMapPtr = framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (naviMapPtr == IntPtr.Zero) return;
            var naviMap = (AtkUnitBase*) naviMapPtr;
            //NOTE (chiv) 3 means fully loaded
            if (naviMap->ULDData.LoadedState != 3) return;
            // TODO (chiv) Check the flag if _NaviMap is hidden in the HUD
            if (!naviMap->IsVisible) return;
            var scale = config.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
            const float windowHeight = 50f;
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
                                           | ImGuiWindowFlags.NoMove
                                           | ImGuiWindowFlags.NoMouseInputs
                                           | ImGuiWindowFlags.NoFocusOnAppearing
                                           | ImGuiWindowFlags.NoBackground
                                           | ImGuiWindowFlags.NoNav
                                           | ImGuiWindowFlags.NoInputs
                                           | ImGuiWindowFlags.NoCollapse;
            ImGui.SetNextWindowSizeConstraints(
                new Vector2(250f, (windowHeight + 20) * scale),
                new Vector2(int.MaxValue, (windowHeight + 20) * scale));
            if (!ImGui.Begin("###ImGuiCompassWindow"
                , configureable
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

            // NOTE (Chiv) This is the position of the player in the minimap coordinate system
            const int playerX = 72, playerY = 72;
            const uint whiteColor = 0xFFFFFFFF;
            var cameraRotationInRadian = -*(float*) (maybeCameraStruct + 0x130);
            var miniMapIconsRootComponentNode = (AtkComponentNode*) naviMap->ULDData.NodeList[2];
            // Minimap rotation thingy is even already flipped!
            // And apparently even accessible & updated if _NaviMap is disabled
            // => This leads to jerky behaviour though
            //var cameraRotationInRadian = miniMapIconsRootComponentNode->Component->ULDData.NodeList[2]->Rotation;
            var scaleFactorForRotationBasedDistance = scale;
            // TODO (Chiv) My math must be bogus somewhere, cause I need to do some things differently then math would say
            // TODO I think my and the games coordinate system do not agree
            // TODO (Chiv) Redo the math for not locked _NaviMap (might be easier? Should be the same?)
            var playerCos = (float) Math.Cos(cameraRotationInRadian);
            var playerSin = (float) Math.Sin(cameraRotationInRadian);
            //TODO (Chiv) Uhm, actually with H=1, it should be new Vector(cos,sin); that breaks calculations though...
            // Is my Coordinate System the same as the games' minimap?
            var playerForward = new Vector2(-playerSin, playerCos);
            var zeroVec = Vector2.Zero;
            var oneVec = Vector2.One;
            // TODO (Chiv) do it all in Radians
            var widthOfCompass = ImGui.GetWindowContentRegionWidth();
            var halfWidthOfCompass = widthOfCompass * 0.5f;
            var halfHeightOfCompass = windowHeight * 0.5f * scale;
            var compassUnit = widthOfCompass / (2f*(float)Math.PI);
            var westCardinalAtkImageNode = (AtkImageNode*) naviMap->ULDData.NodeList[11];
            // TODO (Chiv) Cache on TerritoryChange/Initialisation?
            var naviMapTextureD3D11ShaderResourceView = new IntPtr(
                westCardinalAtkImageNode->PartsList->Parts[0]
                    .ULDTexture->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
            );

            var drawList = ImGui.GetWindowDrawList();
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            var cursorPosition = ImGui.GetCursorScreenPos() - new Vector2(7, 0);
            var compassCentre =
                new Vector2(cursorPosition.X + halfWidthOfCompass, cursorPosition.Y + halfHeightOfCompass);
            var halfWidth32 = 16 * scale;
            var backgroundPMin = new Vector2(compassCentre.X - 5 - halfWidthOfCompass,
                compassCentre.Y - halfHeightOfCompass * 0.5f - 2);
            var backgroundPMax = new Vector2(compassCentre.X + 5 + halfWidthOfCompass,
                compassCentre.Y + halfHeightOfCompass * 0.5f + 2);
            //First, the background
            if (config.ImGuiCompassEnableBackground)
            {
                if (config.ImGuiCompassFillBackground)
                    backgroundDrawList.AddRectFilled(
                        backgroundPMin
                        , backgroundPMax
                        , ImGui.ColorConvertFloat4ToU32(config.ImGuiBackgroundColour)
                        , config.ImGuiCompassBackgroundRounding
                    );
                if (config.ImGuiCompassDrawBorder)
                    backgroundDrawList.AddRect(
                        backgroundPMin - Vector2.One
                        , backgroundPMax + Vector2.One
                        , ImGui.ColorConvertFloat4ToU32(config.ImGuiBackgroundBorderColour)
                        , config.ImGuiCompassBackgroundRounding
                    );
            }

            // Second, we position our Cardinals
            //TODO (Chiv) Uhm, no, east is the other way. Again, coordinate system mismatch?
            var east = -Vector2.UnitX;
            var south = -Vector2.UnitY;
            var west = Vector2.UnitX;
            var north = Vector2.UnitY;
            // TODO (Chiv) Yeah, the minus  here is bogus as hell too.
            // TODO (chiv) actually, SignedAngle first arg is FROM, not TO
            var eastOffset = compassUnit * -SignedAngle(east, playerForward);
            var halfWidth20 = 10 * scale;
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth20 + eastOffset, compassCentre.Y - halfWidth32) // Width = 20
                , new Vector2(compassCentre.X + eastOffset + halfWidth20, compassCentre.Y + halfWidth32)
                , new Vector2(0.5446429f, 0.8301887f)
                , new Vector2(0.5892857f, 0.9811321f)
            );
            var southOffset = compassUnit * -SignedAngle(south, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth20 + southOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + southOffset + halfWidth20, compassCentre.Y + halfWidth32)
                , new Vector2(0.5892857f, 0.8301887f)
                , new Vector2(0.6339286f, 0.9811321f)
            );
            var westOffset = compassUnit * -SignedAngle(west, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth32 + westOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + westOffset + halfWidth32, compassCentre.Y + halfWidth32)
                , new Vector2(0.4732143f, 0.8301887f)
                , new Vector2(0.5446429f, 0.9811321f)
            );
            var northOffset = compassUnit * -SignedAngle(north, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth32 + northOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + northOffset + halfWidth32, compassCentre.Y + halfWidth32)
                , new Vector2(0.4017857f, 0.8301887f)
                , new Vector2(0.4732143f, 0.9811321f)
                , ImGui.ColorConvertFloat4ToU32(new Vector4(176f / 255f, 100f / 255f, 0f, 1))
            );

            try
            {
                // Then, we do the dance through all relevant nodes on _NaviMap
                // I imagine this throws sometimes because of racing conditions -> We try to access an already freed texture e.g.
                // So we just ignore those small exceptions, it works a few frames later anyways
                var mapScale =
                    miniMapIconsRootComponentNode->Component->ULDData.NodeList[1]->ScaleX; //maxZoom level == 2
                var playerPos = new Vector2(playerX, playerY);
                for (var i = 4; i < miniMapIconsRootComponentNode->Component->ULDData.NodeListCount; i++)
                {
                    var mapIconComponentNode =
                        (AtkComponentNode*) miniMapIconsRootComponentNode->Component->ULDData.NodeList[i];
                    if (!mapIconComponentNode->AtkResNode.IsVisible) continue;
                    for (var j = 2; j < mapIconComponentNode->Component->ULDData.NodeListCount; j++)
                    {
                        // NOTE (Chiv) Invariant: From 2 onward, only ImageNodes
                        var imgNode = (AtkImageNode*) mapIconComponentNode->Component->ULDData.NodeList[j];
#if DEBUG
                        if (imgNode->AtkResNode.Type != NodeType.Image)
                        {
                            SimpleLog.Error($"{i}{j} was not an ImageNode");
                            continue;
                        }
#endif
                        if (!imgNode->AtkResNode.IsVisible || !imgNode->AtkResNode.ParentNode->IsVisible) continue;
                        var part = imgNode->PartsList->Parts[imgNode->PartId];
                        //NOTE (CHIV) Invariant: It should always be a resource
#if DEBUG
                        var type = part.ULDTexture->AtkTexture.TextureType;
                        if (type != TextureType.Resource)
                        {
                            SimpleLog.Error($"{i}{j} was not a Resource texture");
                            continue;
                        };
#endif
                        var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                        var texFileNamePtr =
                            part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        // NOTE (Chiv) We are in a try-catch, so we just throw if the read failed.
                        // Cannot act anyways if the texture path is butchered
                        SimpleLog.Log($"Trying to new string with pointer");
                        var textureFileName = new string((sbyte*) texFileNamePtr);
                        SimpleLog.Log($"Read {textureFileName}");
                        
                        //var success = uint.TryParse(textureFileName.Substring(textureFileName.LastIndexOf('/')+1, 6), out var iconId);
                        var _ = uint.TryParse(textureFileName.Substring(textureFileName.Length - 10, 6),
                            out var iconId);
                        //iconId = 0 (==> success == false as IconID will never be 0) Must have been NaviMap (and only that hopefully)
                        var textureIdPtr = new IntPtr(tex->D3D11ShaderResourceView);
                        Vector2 pMin;
                        Vector2 pMax;
                        var uv = zeroVec;
                        var uv1 = oneVec;
                        var tintColour = whiteColor;

                        switch (iconId)
                        {
                            case 0: //Arrows to quests and fates, glowy thingy
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
                                // Arrows and such are always rotation based
                                // TODO (Chiv) Glowing thingy is not
                                goto case 1;
                            case 1: // Rotation Based icons go here after setting up their UVs
                                // TODO Ring, ring, SignedAngle first arg is FROM !
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                var rotationIconOffset = compassUnit *
                                                       -CalculateSignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                           playerForward);
                                // We hope width == height
                                var rotationIconHalfWidth = part.Width * 0.5f * scaleFactorForRotationBasedDistance;
                                pMin = new Vector2(compassCentre.X - rotationIconHalfWidth + rotationIconOffset,
                                    compassCentre.Y - rotationIconHalfWidth);
                                pMax = new Vector2(
                                    compassCentre.X + rotationIconOffset + rotationIconHalfWidth,
                                    compassCentre.Y + rotationIconHalfWidth);
                                break;
                            case 060443: //Player Marker
                                if (!config.ImGuiCompassEnableCenterMarker) continue;
                                drawList = backgroundDrawList;
                                pMin = new Vector2(compassCentre.X - halfWidth32,
                                    compassCentre.Y + config.ImGuiCompassCentreMarkerOffset * scale -
                                    halfWidth32);
                                pMax = new Vector2(compassCentre.X + halfWidth32,
                                    compassCentre.Y + config.ImGuiCompassCentreMarkerOffset * scale +
                                    halfWidth32);
                                uv1 = config.ImGuiCompassFlipCentreMarker ? new Vector2(1, -1) : oneVec;
                                break;
                            case 060495: // Small Area Circle
                            case 060496: // Big Area Circle
                            case 060497: // Another Circle
                            case 060498: // One More Circle
                                bool inArea;
                                (pMin, pMax, tintColour, inArea)
                                    = CalculateAreaCirlceVariables(playerPos, playerForward, mapIconComponentNode,
                                        imgNode, mapScale, compassUnit, halfWidth32, compassCentre);
                                if (inArea)
                                    //*((byte*) &tintColour + 3) = 0x33  == (0x33FFFFFF) & (tintColour)
                                    backgroundDrawList.AddRectFilled(
                                        backgroundPMin
                                        , backgroundPMax
                                        , 0x33FFFFFF & tintColour //Set A to 0.2
                                        , config.ImGuiCompassBackgroundRounding
                                    );
                                break;
                            case 060542: // Arrow UP on Circle
                            case 060546: // Arrow DOWN on Circle
                                (pMin, pMax, tintColour, _)
                                    = CalculateAreaCirlceVariables(playerPos, playerForward, mapIconComponentNode,
                                        imgNode, mapScale, compassUnit, halfWidth32, compassCentre);
                                break;
                            case 071023: // Quest Ongoing Marker
                            case 071025: // Quest Complete Marker
                            case 071063: // BookQuest Ongoing Marker
                            case 071065: // BookQuest Complete Marker
                            case 071083: // LeveQuest Ongoing Marker
                            case 071085: // LeveQuest Complete Marker
                            case 071143: // BlueQuest Ongoing Marker
                            case 071145: // BlueQuest Complete Marker
                                if (mapIconComponentNode->AtkResNode.Rotation == 0)
                                    // => The current quest marker is inside the mask and should be
                                    // treated as a map point
                                    goto default;
                                // => The current quest marker is outside the mask and should be
                                // treated as a rotation
                                goto case 1; //No UV setup needed for quest markers
                            case 060457: // Area Transition Bullet Thingy
                            default:
                                var (iconScaleFactor, iconAngle, _) = CalculateDrawVariables(
                                    playerPos,
                                    new Vector2(
                                        mapIconComponentNode->AtkResNode.X,
                                        mapIconComponentNode->AtkResNode.Y
                                    ),
                                    playerForward,
                                    mapScale);
                                // TODO Ring, ring, SignedAngle first arg is FROM !
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                // NOTE (Chiv) We assume part.Width == part.Height == 32
                                var iconOffset = compassUnit * -iconAngle;
                                var iconHalfWidth = halfWidth32 * iconScaleFactor;
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

                //TODO (Chiv) Later, we might do that for AreaMap too
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

        private unsafe void UpdateHideCompass(Configuration config, Framework framework, Condition condition)
        {
            for (var i = 0; i < UiObjectsCheckPerFrame; i++)
            {
                var uiIdentifier = _uiIdentifiers[_currentUiObjectIndex++];
                _currentUiObjectIndex %= _uiIdentifiers.Length;
                if (_currentUiObjectIndex == 0)
                {
                    _shouldHideCompassIteration |= config.HideInCombat && condition[ConditionFlag.InCombat];
                    _shouldHideCompass = _shouldHideCompassIteration;
                    _shouldHideCompassIteration = false;
                }
                var unitBase = framework.Gui.GetUiObjectByName(uiIdentifier, 1);
                if (unitBase == IntPtr.Zero) continue;
                _shouldHideCompassIteration |= ((AtkUnitBase*) unitBase)->IsVisible;
                                
            }
        }
        


        #region Math related methods
        
             [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe (Vector2 pMin, Vector2 pMax, uint tintColour, bool inArea) CalculateAreaCirlceVariables(
            Vector2 playerPos, Vector2 playerForward, AtkComponentNode* mapIconComponentNode,
            AtkImageNode* imgNode, float mapScale, float compassUnit, float halfWidth32, Vector2 compassCentre)
        {
            // TODO Distinguish between Circles for quests and circles for Fates (colour?) for filtering
            var (scaleArea, angleArea, distanceArea) = CalculateDrawVariables(
                playerPos,
                new Vector2(
                    mapIconComponentNode->AtkResNode.X,
                    mapIconComponentNode->AtkResNode.Y
                ),
                playerForward,
                mapScale);
            //TODO Adjust for different scale levels...or not.
            var radius = mapIconComponentNode->AtkResNode.ScaleX *
                         (mapIconComponentNode->AtkResNode.Width - mapIconComponentNode->AtkResNode.OriginX);
            // TODO Ring, ring, SignedAngle first arg is FROM !
            // TODO (Chiv) Ehhh, the minus before SignedAngle
            // NOTE (Chiv) We assume part.Width == part.Height == 32
            var areaCircleOffset = compassUnit * -angleArea;
            var areaHalfWidth = halfWidth32 * scaleArea;
            if (distanceArea >= radius)
                return (
                    new Vector2(compassCentre.X - areaHalfWidth + areaCircleOffset, compassCentre.Y - areaHalfWidth),
                    new Vector2(compassCentre.X + areaCircleOffset + areaHalfWidth, compassCentre.Y + areaHalfWidth),
                    0xFFFFFFFF, false);
            var tintColour = ImGui.ColorConvertFloat4ToU32(new Vector4(
                (255 + imgNode->AtkResNode.AddRed) * (imgNode->AtkResNode.MultiplyRed / 100f) / 255f,
                (255 + imgNode->AtkResNode.AddGreen) * (imgNode->AtkResNode.MultiplyGreen / 100f) / 255f,
                (255 + imgNode->AtkResNode.AddBlue) * (imgNode->AtkResNode.MultiplyBlue / 100f) / 255f,
                1));
            return (new Vector2(compassCentre.X - areaHalfWidth, compassCentre.Y - areaHalfWidth)
                , new Vector2(compassCentre.X + areaHalfWidth, compassCentre.Y + areaHalfWidth)
                , tintColour, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (float scaleFactor, float signedAngle, float distance) CalculateDrawVariables(Vector2 from,
            Vector2 to, Vector2 forward, float distanceScaling)
        {
            const float lowestScaleFactor = 0.3f;
            // TODO (Chiv) Distance Offset adjustments
            var distanceOffset = 40f * distanceScaling; //80f @Max Zoom(==2) _NaviMap
            var maxDistance = 180f * distanceScaling; //360f @Max Zoom(==2) _NaviMap
            //TODO (Chiv) Oh boy, check the math
            var distance = Vector2.Distance(to, from);
            var scaleFactor = Math.Max(1f - (distance - distanceOffset) / maxDistance, lowestScaleFactor);
            //return (scaleFactor,SignedAngle(to  - from, forward), distance);
            return (scaleFactor, SignedAngle(from - to, forward), distance);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateSignedAngle(in float rotation, in Vector2 forward)
        {
            var cosObject = (float) Math.Cos(rotation);
            var sinObject = (float) Math.Sin(rotation);
            // TODO Wrong math!
            var objectForward = new Vector2(-sinObject, cosObject);
            return SignedAngle(objectForward, forward);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SignedAngle(Vector2 from, Vector2 to)
        {
            var dot = Vector2.Dot(Vector2.Normalize(from), Vector2.Normalize(to));
            var sign = (from.X * to.Y - from.Y * to.X) >= 0 ? 1 : -1;
            return (float)Math.Acos(dot) * sign;
        }
        
        #endregion
    }
}