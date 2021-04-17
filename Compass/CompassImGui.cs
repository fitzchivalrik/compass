using System;
using System.Numerics;
using Dalamud.Game.ClientState;
using Dalamud.Interface;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin;

namespace Compass
{
    public partial class Compass
    {
        
        // TODO Cut this s@>!? in smaller methods
        private unsafe void BuildImGuiCompassNavi()
        {
            if (!_config.ImGuiCompassEnable) return;
            if (_config.HideInCombat && _pluginInterface.ClientState.Condition[ConditionFlag.InCombat]) return;
            UpdateHideCompass();
            if (_shouldHideCompass) return;
            var naviMapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (naviMapPtr == IntPtr.Zero) return;
            var naviMap = (AtkUnitBase*) naviMapPtr;
            //NOTE (chiv) 3 means fully loaded
            if (naviMap->ULDData.LoadedState != 3) return;
            // TODO (chiv) Check the flag if _NaviMap is hidden in the HUD
            if (!naviMap->IsVisible) return;
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
            // NOTE (Chiv) This is the position of the player in the minimap coordinate system
            // It has positive down Y grow, we do calculations in a 'default' coordinate system
            // with positive up Y grow
            // => All game Y needs to be flipped.
            const int playerX = 72, playerY = -72;
            const uint whiteColor = 0xFFFFFFFF;
            // 0 == Facing North, -PI/2 facing east, PI/2 facing west.
            var cameraRotationInRadian = *(float*) (_maybeCameraStruct + 0x130);
            var miniMapIconsRootComponentNode = (AtkComponentNode*) naviMap->ULDData.NodeList[2];
            // Minimap rotation thingy is even already flipped!
            // And apparently even accessible & updated if _NaviMap is disabled
            // => This leads to jerky behaviour though
            //var cameraRotationInRadian = miniMapIconsRootComponentNode->Component->ULDData.NodeList[2]->Rotation;
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
            //A little offset due to padding.
            var cursorPosition = ImGui.GetCursorScreenPos();
            var compassCentre =
                new Vector2(cursorPosition.X + halfWidthOfCompass, cursorPosition.Y + halfHeightOfCompass);
            var halfWidth32 = 16 * scale;
            var backgroundPMin = new Vector2(compassCentre.X - 5 - halfWidthOfCompass,
                compassCentre.Y - halfHeightOfCompass * 0.5f - 2);
            var backgroundPMax = new Vector2(compassCentre.X + 5 + halfWidthOfCompass,
                compassCentre.Y + halfHeightOfCompass * 0.5f + 2);
            //First, the background
            DrawImGuiCompassBackground( backgroundPMin, backgroundPMax);
            // Second, we position our Cardinals
            var westCardinalAtkImageNode = (AtkImageNode*) naviMap->ULDData.NodeList[11];
            // TODO (Chiv) Cache on TerritoryChange/Initialisation?
            var naviMapTextureD3D11ShaderResourceView = new IntPtr(
                westCardinalAtkImageNode->PartsList->Parts[0]
                    .ULDTexture->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
            );
            DrawCardinals(compassUnit, playerForward, scale, naviMapTextureD3D11ShaderResourceView, compassCentre, halfWidth32);

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
                        if (imgNode->AtkResNode.Type != NodeType.Image)
                        {
                            continue;
                        }
                        if (!imgNode->AtkResNode.IsVisible || !imgNode->AtkResNode.ParentNode->IsVisible) continue;
                        var part = imgNode->PartsList->Parts[imgNode->PartId];
                        //NOTE (CHIV) Invariant: It should always be a resource
#if DEBUG
                        var type = part.ULDTexture->AtkTexture.TextureType;
                        if (type != TextureType.Resource)
                        {
                            SimpleLog.Error($"{i} {j} was not a Resource texture");
                            continue;
                        };
#endif
                        var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                        var texFileNamePtr =
                            part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        // NOTE (Chiv) We are in a try-catch, so we just throw if the read failed.
                        // Cannot act anyways if the texture path is butchered
                        var textureFileName = new string((sbyte*) texFileNamePtr);
                        //var success = uint.TryParse(textureFileName.Substring(textureFileName.LastIndexOf('/')+1, 6), out var iconId);
                        var _ = uint.TryParse(textureFileName.Substring(textureFileName.LastIndexOfAny(new []{'/', '\\'})+1, 6),
                            out var iconId);
                        //iconId = 0 (==> success == false as IconID will never be 0) Must have been NaviMap (and only that hopefully)
                        var textureIdPtr = new IntPtr(tex->D3D11ShaderResourceView);
                        Vector2 pMin;
                        Vector2 pMax;
                        var uv = zeroVec;
                        var uv1 = oneVec;
                        var tintColour = whiteColor;

                        // TODO (Chiv) Multi-key-single value map? stringHash as key?
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
                                    = CalculateAreaCirlceVariables(playerPos, playerForward, mapIconComponentNode,
                                        imgNode, mapScale, compassUnit, halfWidth32, compassCentre);
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
                            case 060954: // Arrow up for quests
                            case 060955: // Arrow down for quests
                            case 060561: // Red Flag (Custom marker)
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
                                var (distanceScaleFactor, iconAngle, _) = CalculateDrawVariables(
                                    playerPos,
                                    new Vector2(
                                        mapIconComponentNode->AtkResNode.X,
                                        -mapIconComponentNode->AtkResNode.Y
                                    ),
                                    playerForward,
                                    mapScale
                                );
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
            DrawImGuiCompassBackground( backgroundPMin, backgroundPMax);
            // Second, we position our Cardinals
            var rotationTriangleImageNode = (AtkImageNode*) areaMapIconsRootComponentNode->Component->ULDData.NodeList[5];
            // TODO (Chiv) Cache on TerritoryChange/Initialisation?
            var naviMapTextureD3D11ShaderResourceView = new IntPtr(
                rotationTriangleImageNode->PartsList->Parts[0]
                    .ULDTexture->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
            );
            DrawCardinals(compassUnit, playerForward, scale, naviMapTextureD3D11ShaderResourceView, compassCentre, halfWidth32);
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
                        var type = part.ULDTexture->AtkTexture.TextureType;
                        if (type != TextureType.Resource)
                        {
                            SimpleLog.Error($"{i} {j} was not a Resource texture");
                            continue;
                        };
#endif
                        var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                        var texFileNamePtr =
                            part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
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
        }
        
        
        private static void DrawCardinals(float compassUnit, Vector2 playerForward, float scale,
            IntPtr naviMapTextureD3D11ShaderResourceView, Vector2 compassCentre,
            float halfWidth32)
        {
            var backgroundDrawList = ImGui.GetBackgroundDrawList();

            var east = Vector2.UnitX;
            var south = -Vector2.UnitY;
            var west = -Vector2.UnitX;
            var north = Vector2.UnitY;
            var eastOffset = compassUnit * SignedAngle(east, playerForward);
            var halfWidth20 = 10 * scale;
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth20 + eastOffset, compassCentre.Y - halfWidth32) // Width = 20
                , new Vector2(compassCentre.X + eastOffset + halfWidth20, compassCentre.Y + halfWidth32)
                , new Vector2(0.5446429f, 0.8301887f)
                , new Vector2(0.5892857f, 0.9811321f)
            );
            var southOffset = compassUnit * SignedAngle(south, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth20 + southOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + southOffset + halfWidth20, compassCentre.Y + halfWidth32)
                , new Vector2(0.5892857f, 0.8301887f)
                , new Vector2(0.6339286f, 0.9811321f)
            );
            var westOffset = compassUnit * SignedAngle(west, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth32 + westOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + westOffset + halfWidth32, compassCentre.Y + halfWidth32)
                , new Vector2(0.4732143f, 0.8301887f)
                , new Vector2(0.5446429f, 0.9811321f)
            );
            var northOffset = compassUnit * SignedAngle(north, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth32 + northOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + northOffset + halfWidth32, compassCentre.Y + halfWidth32)
                , new Vector2(0.4017857f, 0.8301887f)
                , new Vector2(0.4732143f, 0.9811321f)
                , 0xFF0064B0 //ABGR ImGui.ColorConvertFloat4ToU32(new Vector4(176f / 255f, 100f / 255f, 0f, 1)) // TODO Static const
            );
        }
        
        private void DrawImGuiCompassBackground(Vector2 backgroundPMin,
            Vector2 backgroundPMax)
        {
            if (!_config.ImGuiCompassEnableBackground) return;
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            if (_config.ImGuiCompassFillBackground)
                backgroundDrawList.AddRectFilled(
                    backgroundPMin
                    , backgroundPMax
                    , _config.ImGuiBackgroundColourUInt32
                    , _config.ImGuiCompassBackgroundRounding
                );
            if (_config.ImGuiCompassDrawBorder)
                backgroundDrawList.AddRect(
                    backgroundPMin - Vector2.One
                    , backgroundPMax + Vector2.One
                    , _config.ImGuiBackgroundBorderColourUInt32
                    , _config.ImGuiCompassBackgroundRounding
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