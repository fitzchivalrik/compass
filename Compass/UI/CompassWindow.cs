using System;
using System.Collections.Generic;
using System.Numerics;
using Compass.Data;
using Dalamud.Interface;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace Compass.UI;

internal static class CompassWindow
{
    internal static Vector2 Draw(
        DrawVariables drawVariables
      , Pointers      pointers
      , float         cameraRotationInRadian
      , Vector2       playerPosition
      , Configuration config
    )
    {
        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoMouseInputs
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoInputs
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoSavedSettings;
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(config.ImGuiCompassPosition, ImGuiCond.Always);
        ImGui.Begin("###ImGuiCompassWindow", flags);

        var cosPlayer = MathF.Cos(cameraRotationInRadian);
        var sinPlayer = MathF.Sin(cameraRotationInRadian);
        // NOTE: Interpret game's camera rotation as
        // 0 => (0,1) (North), PI/2 => (-1,0) (West)  in default coordinate system
        // Games Map coordinate system origin is upper left, with positive Y grow
        var playerForward      = new Vector2(-sinPlayer, cosPlayer);
        var drawList           = ImGui.GetWindowDrawList();
        var backgroundDrawList = ImGui.GetBackgroundDrawList();
        drawList.PushClipRect(drawVariables.DrawListPMin, drawVariables.DrawListPMax);
        backgroundDrawList.PushClipRect(drawVariables.BackgroundDrawListPMin, drawVariables.BackgroundDrawListPMax);

        DrawImGuiCompassBackground(
            drawVariables.BackgroundPMin,
            drawVariables.BackgroundPMax,
            drawVariables.BackgroundLinePMin,
            drawVariables.BackgroundLinePMax,
            drawVariables.BackgroundColourUInt32,
            drawVariables.BackgroundBorderColourUInt32,
            drawVariables.BackgroundLineColourUInt32,
            config
        );
        if (config.ShowInterCardinals)
        {
            DrawInterCardinals(
                playerForward,
                drawVariables.Centre,
                drawVariables.HalfWidth28,
                drawVariables.CompassUnit,
                config.ImGuiCompassCardinalsOffset,
                pointers.NaviMapTextureD3D11ShaderResourceView
            );
        }

        if (config.ShowCardinals)
        {
            DrawCardinals(
                playerForward,
                drawVariables.Centre,
                drawVariables.HalfWidth40,
                drawVariables.HalfWidth28,
                drawVariables.CompassUnit,
                config.ImGuiCompassCardinalsOffset,
                pointers.NaviMapTextureD3D11ShaderResourceView
            );
        }

        if (config.ShowWeatherIcon)
        {
            unsafe
            {
                DrawWeatherIcon(
                    drawVariables.WeatherIconBorderPMin,
                    drawVariables.WeatherIconBorderPMax,
                    drawVariables.WeatherIconPMin,
                    drawVariables.WeatherIconPMax,
                    pointers.NaviMapTextureD3D11ShaderResourceView,
                    pointers.WeatherIconNode
                );
            }
        }

        if (config.ShowDistanceToTarget)
        {
            unsafe
            {
                DrawDistanceToTarget(
                    config.ImGuiCompassDistanceToTargetMouseOverPrio,
                    pointers.TargetSystem,
                    drawVariables.DistanceToTargetScale,
                    drawVariables.DistanceToTargetColourUInt32,
                    drawVariables.DistanceToTargetPMin,
                    config.DistanceToTargetPrefix,
                    config.DistanceToTargetSuffix
                );
            }
        }

        if (!config.ShowOnlyCardinals)
        {
            unsafe
            {
                playerPosition = DrawIcons(
                    drawVariables.CurrentScaleOffset,
                    drawVariables.ComponentIconLoopStart,
                    drawVariables.ComponentIconLoopEnd,
                    config.ImGuiCompassCentreMarkerOffset,
                    drawVariables.MaxDistance,
                    drawVariables.CompassUnit,
                    drawVariables.Scale,
                    drawVariables.RotationIconHalfWidth,
                    drawVariables.HalfWidth40,
                    drawVariables.MinScaleFactor,
                    config.ImGuiCompassBackgroundRounding,
                    playerForward,
                    playerPosition,
                    drawVariables.Centre,
                    drawVariables.BackgroundPMin,
                    drawVariables.BackgroundPMax,
                    config.UseAreaMapAsSource,
                    config.ImGuiCompassEnableCenterMarker,
                    config.ImGuiCompassFlipCentreMarker,
                    pointers.CurrentSourceBase,
                    pointers.CurrentMapIconsRootComponentNode,
                    config.FilteredIconIds
                );
            }
        }

        drawList.PopClipRect();
        backgroundDrawList.PopClipRect();
        ImGui.End();
        return playerPosition;
    }

    private static void DrawImGuiCompassBackground(
        Vector2       backgroundPMin
      , Vector2       backgroundPMax
      , Vector2       backgroundLinePMin
      , Vector2       backgroundLinePMax
      , uint          backgroundColour
      , uint          backgroundBorderColour
      , uint          backgroundLineColour
      , Configuration config
    )
    {
        if (!config.ImGuiCompassEnableBackground) return;
        var backgroundDrawList = ImGui.GetBackgroundDrawList();
        if (config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Filled or ImGuiCompassBackgroundStyle.FilledAndBorder)
        {
            backgroundDrawList.AddRectFilled(backgroundPMin
              , backgroundPMax
              , backgroundColour
              , config.ImGuiCompassBackgroundRounding
            );
        }

        switch (config.ImGuiCompassBackground)
        {
            case ImGuiCompassBackgroundStyle.Border or ImGuiCompassBackgroundStyle.FilledAndBorder:
                backgroundDrawList.AddRect(backgroundPMin - Vector2.One
                  , backgroundPMax + Vector2.One
                  , backgroundBorderColour
                  , config.ImGuiCompassBackgroundRounding
                  , ImDrawFlags.RoundCornersAll
                  , config.ImGuiBackgroundBorderThickness
                );
                break;
            case ImGuiCompassBackgroundStyle.Line:
                backgroundDrawList.AddLine(backgroundLinePMin
                  , backgroundLinePMax
                  , backgroundLineColour
                  , config.ImGuiBackgroundLineThickness
                );
                break;
        }
    }

    private static void DrawInterCardinals(
        Vector2 playerForward
      , Vector2 centre
      , float   halfWidth28
      , float   compassUnit
      , int     cardinalsOffset
      , nint    naviMapTextureD3D11ShaderResourceView
    )
    {
        var         backgroundDrawList = ImGui.GetBackgroundDrawList();
        const float n                  = 0.7071067811865475f;
        // const float n  = 0.5f;
        var northeast = new Vector2(n, n);
        var southeast = new Vector2(n, -n);
        var southwest = new Vector2(-n, -n);
        var northwest = new Vector2(-n, n);

        var quarterWidth28  = 0.5f * halfWidth28;
        var pMinY           = centre.Y - halfWidth28 + cardinalsOffset;
        var pMaxY           = centre.Y + halfWidth28 + cardinalsOffset;
        var northeastOffset = compassUnit * Util.SignedAngle(northeast, playerForward);
        // TODO The numbers must be scaled and as such precalcualted
        var pMinXLeft  = centre.X - 3 * quarterWidth28;
        var pMaxXLeft  = centre.X + quarterWidth28;
        var pMinXRight = centre.X - quarterWidth28;
        var pMaxXRight = centre.X + 3 * quarterWidth28;
        backgroundDrawList.AddImage( // North
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(pMinXLeft + northeastOffset, pMinY)
          , new Vector2(pMaxXLeft + northeastOffset, pMaxY)
          , new Vector2(0.4017857f, 0.8301887f)
          , new Vector2(0.4732143f, 0.9811321f)
        );
        backgroundDrawList.AddImage( // East
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(pMinXRight + northeastOffset, pMinY)
          , new Vector2(pMaxXRight + northeastOffset, pMaxY)
          , new Vector2(0.5446429f, 0.8301887f)
          , new Vector2(0.5892857f, 0.9811321f)
        );
        var southeastOffset = compassUnit * Util.SignedAngle(southeast, playerForward);
        backgroundDrawList.AddImage( //South
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(pMinXLeft + southeastOffset, pMinY)
          , new Vector2(pMaxXLeft + southeastOffset, pMaxY)
          , new Vector2(0.5892857f, 0.8301887f)
          , new Vector2(0.6339286f, 0.9811321f)
        );
        backgroundDrawList.AddImage( // East
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(pMinXRight + southeastOffset, pMinY)
          , new Vector2(pMaxXRight + southeastOffset, pMaxY)
          , new Vector2(0.5446429f, 0.8301887f)
          , new Vector2(0.5892857f, 0.9811321f)
        );
        var southwestOffset = compassUnit * Util.SignedAngle(southwest, playerForward);
        backgroundDrawList.AddImage( //South
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(pMinXLeft + southwestOffset, pMinY)
          , new Vector2(pMaxXLeft + southwestOffset, pMaxY)
          , new Vector2(0.5892857f, 0.8301887f)
          , new Vector2(0.6339286f, 0.9811321f)
        );
        backgroundDrawList.AddImage( // West
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(pMinXRight + southwestOffset, pMinY)
          , new Vector2(pMaxXRight + southwestOffset, pMaxY)
          , new Vector2(0.4732143f, 0.8301887f)
          , new Vector2(0.5446429f, 0.9811321f)
        );
        var northwestOffset = compassUnit * Util.SignedAngle(northwest, playerForward);
        backgroundDrawList.AddImage( // North
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(pMinXLeft + northwestOffset, pMinY)
          , new Vector2(pMaxXLeft + northwestOffset, pMaxY)
          , new Vector2(0.4017857f, 0.8301887f)
          , new Vector2(0.4732143f, 0.9811321f)
        );
        backgroundDrawList.AddImage( // West
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(pMinXRight + northwestOffset, pMinY)
          , new Vector2(pMaxXRight + northwestOffset, pMaxY)
          , new Vector2(0.4732143f, 0.8301887f)
          , new Vector2(0.5446429f, 0.9811321f)
        );
    }

    private static void DrawCardinals(
        Vector2 playerForward
      , Vector2 centre
      , float   halfWidth40
      , float   halfWidth28
      , float   compassUnit
      , int     cardinalsOffset
      , nint    naviMapTextureD3D11ShaderResourceView
    )
    {
        var backgroundDrawList = ImGui.GetBackgroundDrawList();

        var east  = Vector2.UnitX;
        var south = -Vector2.UnitY;
        var west  = -Vector2.UnitX;
        var north = Vector2.UnitY;

        var pMinY      = centre.Y - halfWidth40 + cardinalsOffset;
        var pMaxY      = centre.Y + halfWidth40 + cardinalsOffset;
        var eastOffset = compassUnit * Util.SignedAngle(east, playerForward);
        backgroundDrawList.AddImage( //East
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(centre.X - halfWidth28 + eastOffset, pMinY)
          , new Vector2(centre.X + eastOffset + halfWidth28, pMaxY)
          , new Vector2(0.5446429f, 0.8301887f)
          , new Vector2(0.5892857f, 0.9811321f)
        );
        var southOffset = compassUnit * Util.SignedAngle(south, playerForward);
        backgroundDrawList.AddImage( // South
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(centre.X - halfWidth28 + southOffset, pMinY)
          , new Vector2(centre.X + southOffset + halfWidth28, pMaxY)
          , new Vector2(0.5892857f, 0.8301887f)
          , new Vector2(0.6339286f, 0.9811321f)
        );
        var westOffset = compassUnit * Util.SignedAngle(west, playerForward);
        backgroundDrawList.AddImage( //West
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(centre.X - halfWidth40 + westOffset, pMinY)
          , new Vector2(centre.X + westOffset + halfWidth40, pMaxY)
          , new Vector2(0.4732143f, 0.8301887f)
          , new Vector2(0.5446429f, 0.9811321f)
        );
        var northOffset = compassUnit * Util.SignedAngle(north, playerForward);
        backgroundDrawList.AddImage( // North
            naviMapTextureD3D11ShaderResourceView
          , new Vector2(centre.X - halfWidth40 + northOffset, pMinY)
          , new Vector2(centre.X + northOffset + halfWidth40, pMaxY)
          , new Vector2(0.4017857f, 0.8301887f)
          , new Vector2(0.4732143f, 0.9811321f)
          , 0xFF0064B0 //ABGR ImGui.ColorConvertFloat4ToU32(new Vector4(176f / 255f, 100f / 255f, 0f, 1))
        );
    }

    private static unsafe void DrawWeatherIcon(
        Vector2       weatherIconBorderPMin
      , Vector2       weatherIconBorderPMax
      , Vector2       weatherIconPMin
      , Vector2       weatherIconPMax
      , nint          naviMapTextureD3D11ShaderResourceView
      , AtkImageNode* weatherIconNode
    )
    {
        var backgroundDrawList = ImGui.GetBackgroundDrawList();
        backgroundDrawList.PushClipRectFullScreen();
        try
        {
            if (!weatherIconNode->AtkResNode.IsVisible) return;
            //Background of Weather Icon
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
              , weatherIconBorderPMin
              , weatherIconBorderPMax
              , new Vector2(0.08035714f, 0.8301887f)
              , new Vector2(0.1607143f, 1));
            //Weather Icon
            var tex = weatherIconNode->PartsList->Parts[0].UldAsset->AtkTexture.Resource->KernelTextureObject;
            backgroundDrawList.AddImage(
                new nint(tex->D3D11ShaderResourceView), weatherIconPMin,
                weatherIconPMax);
            //Border around Weather Icon
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
              , weatherIconBorderPMin
              , weatherIconBorderPMax
              , new Vector2(0.1607143f, 0.8301887f)
              , new Vector2(0.2410714f, 1));
        }
#if DEBUG
        catch (Exception e) {
            PluginLog.Error(e.ToString());
        }
#else
        catch
        {
            // ignored
        }
#endif
        backgroundDrawList.PopClipRect();
    }

    private static unsafe void DrawDistanceToTarget(
        bool          prioritizeMouseOverTarget
      , TargetSystem* targetSystem
      , float         scale
      , uint          colour
      , Vector2       pMin
      , string        prefix
      , string        suffix
    )
    {
        var drawList = ImGui.GetWindowDrawList();
        var current = prioritizeMouseOverTarget && targetSystem->MouseOverTarget != null
            ? targetSystem->MouseOverTarget
            : targetSystem->GetCurrentTarget();
        if (current is null) return;
        var distanceFromPlayer = *current switch
        {
            { ObjectKind: var o } when
                (ObjectKind)o is ObjectKind.Pc or ObjectKind.BattleNpc or ObjectKind.EventNpc
                => current->YalmDistanceFromPlayerX
          , _ => byte.MaxValue
        };
        if (distanceFromPlayer == byte.MaxValue) return;
        var text                  = $"{prefix}{distanceFromPlayer + 1}{suffix}";
        var font                  = UiBuilder.DefaultFont;
        var distanceToTargetScale = font.FontSize * scale;
        // NOTE: Manually unrolled loop
        // Imitates a black border around text
        // A quick'n'dirty solution to be used sparingly
        drawList.AddText(font,
            distanceToTargetScale,
            pMin + new Vector2(-1, +1)
          , 0xFF000000,
            text);
        drawList.AddText(font,
            distanceToTargetScale,
            pMin + new Vector2(0, +1)
          , 0xFF000000
          , text);
        drawList.AddText(font,
            distanceToTargetScale,
            pMin + new Vector2(+1, +1)
          , 0xFF000000,
            text);
        drawList.AddText(font,
            distanceToTargetScale,
            pMin + new Vector2(-1, 0)
          , 0xFF000000,
            text);
        drawList.AddText(font,
            distanceToTargetScale,
            pMin + new Vector2(+1, 0)
          , 0xFF000000,
            text);
        drawList.AddText(font,
            distanceToTargetScale,
            pMin + new Vector2(-1, -1)
          , 0xFF000000,
            text);
        drawList.AddText(font,
            distanceToTargetScale,
            pMin + new Vector2(0, -1)
          , 0xFF000000,
            text);
        drawList.AddText(font,
            distanceToTargetScale,
            pMin + new Vector2(+1, -1)
          , 0xFF000000,
            text);
        drawList.AddText(font, distanceToTargetScale, pMin, colour, text);
    }

    // TODO: Reduce parameter counts by splitting DrawVariables in smaller structs?
    private static unsafe Vector2 DrawIcons(
        int                currentScaleOffset
      , int                componentIconLoopStart
      , int                componentIconLoopEnd
      , int                centreMarkerOffset
      , float              maxDistance
      , float              compassUnit
      , float              scale
      , float              rotationIconHalfWidth
      , float              halfWidth40
      , float              minScaleFactor
      , float              backgroundRounding
      , Vector2            playerForward
      , Vector2            playerPosition
      , Vector2            centre
      , Vector2            backgroundPMin
      , Vector2            backgroundPMax
      , bool               useAreaMapAsSource
      , bool               enableCentreMarker
      , bool               flipCentreMarker
      , AtkUnitBase*       unitBase
      , AtkComponentNode*  rootComponentNode
      , IReadOnlySet<uint> filteredIconIds
    )
    {
        try
        {
            var drawList = ImGui.GetWindowDrawList();
            // We loop through all relevant nodes on _NaviMap or AreaMap, depending on the configuration
            // This throws sometimes, but we just ignore those small exceptions, it works a few frames later anyways
            var mapScale       = *(float*)((nint)unitBase + currentScaleOffset);
            var distanceOffset = 20f * mapScale;
            maxDistance *= mapScale;
            for (var i = componentIconLoopStart; i < rootComponentNode->Component->UldManager.NodeListCount; i++)
            {
                var mapIconComponentNode =
                    (AtkComponentNode*)rootComponentNode->Component->UldManager.NodeList[i];
                if (!mapIconComponentNode->AtkResNode.IsVisible) continue;
                for (var j = 2; j < componentIconLoopEnd; j++)
                {
                    var imgNode = (AtkImageNode*)mapIconComponentNode->Component->UldManager.NodeList[j];
                    if (imgNode->AtkResNode.Type != NodeType.Image) continue;
                    if (!imgNode->AtkResNode.IsVisible || !imgNode->AtkResNode.ParentNode->IsVisible) continue;
                    var part = imgNode->PartsList->Parts[imgNode->PartId];
                    //NOTE Invariant: It should always be a resource
#if DEBUG
                    var type = part.UldAsset->AtkTexture.TextureType;
                    if (type != TextureType.Resource) {
                        PluginLog.Error($"{i} {j} was not a Resource texture");
                        continue;
                    }

                    ;
#endif
                    var tex = part.UldAsset->AtkTexture.Resource->KernelTextureObject;
                    var texFileNameStdString =
                        part.UldAsset->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                           .FileName;
                    // NOTE (Chiv) We are in a try-catch, so we just throw if the read failed.
                    // Cannot act anyways if the texture path is butchered
                    var textureFileName = texFileNameStdString.ToString();
                    var _ = uint.TryParse(
                        textureFileName.AsSpan(textureFileName.LastIndexOfAny(new[] { '/', '\\' }) + 1, 6),
                        out var iconId);
                    //iconId = 0 (=> success == false as IconID will never be 0) Must have been 'NaviMap(_hr1)?\.tex' (and only that hopefully)
                    if (filteredIconIds.Contains(iconId)) continue;

                    var textureIdPtr = new nint(tex->D3D11ShaderResourceView);

                    Vector2 pMin;
                    Vector2 pMax;
                    var     uv         = Vector2.Zero;
                    var     uv1        = Vector2.One;
                    var     tintColour = Constant.WhiteColour;
                    var     rotate     = false;
                    switch (iconId)
                    {
                        case 0 when useAreaMapAsSource: //0 interpreted as NaviMap texture atlas
                            continue;
                        case 0 when imgNode->PartId == 21: //Glowy thingy
                            rotate = true;                 // TODO Duplicate code instead of branching?
                            uv     = new Vector2((float)part.U / 448, (float)part.V / 212);
                            uv1    = new Vector2((float)(part.U + 40) / 448, (float)(part.V + 40) / 212);
                            // NOTE (Chiv) Glowy thingy always rotates, but whether its in or outside the mask
                            // determines how to calculate its position on the compass
                            if (mapIconComponentNode->AtkResNode.Rotation == 0)
                            {
                                goto default;
                            }

                            goto case 1;
                        case 0: //Arrows to quests and fates, part of the Navimap texture atlas
                            // NOTE: We assume part.Width == part.Height == 24
                            // NOTE: We assume tex.Width == 448 && tex.Height == 212
                            var u  = (float)part.U / 448;        // = (float) part.U / tex->Width;
                            var v  = (float)part.V / 212;        // = (float) part.V / tex->Height;
                            var u1 = (float)(part.U + 24) / 448; // = (float) (part.U + part.Width) / tex->Width;
                            var v1 = (float)(part.V + 24) / 212; // = (float) (part.V + part.Height) / tex->Height;

                            uv  = new Vector2(u, v);
                            uv1 = new Vector2(u1, v1);
                            // Arrows and such are always rotation based, we draw them slightly on top
                            var naviMapCutIconOffset = compassUnit *
                                                       Util.SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                           playerForward);
                            // We declare width == height
                            const int naviMapIconHalfWidth = 12;
                            var       naviMapYOffset       = 14 * scale;
                            pMin = new Vector2(centre.X - naviMapIconHalfWidth + naviMapCutIconOffset,
                                centre.Y - naviMapYOffset - naviMapIconHalfWidth);
                            pMax = new Vector2(centre.X + naviMapCutIconOffset + naviMapIconHalfWidth,
                                centre.Y - naviMapYOffset + naviMapIconHalfWidth);
                            break;
                        case 1: // Rotation icons (except naviMap arrows) go here after setting up their UVs
                            // NOTE (Chiv) Rotations for icons on the map are mirrored from the
                            var rotationIconOffset = compassUnit *
                                                     Util.SignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                         playerForward);
                            pMin = new Vector2(centre.X - rotationIconHalfWidth + rotationIconOffset,
                                centre.Y - rotationIconHalfWidth);
                            pMax = new Vector2(centre.X + rotationIconOffset + rotationIconHalfWidth,
                                centre.Y + rotationIconHalfWidth);
                            break;
                        case 060443: //Player Marker
                            if (!enableCentreMarker) continue;
                            drawList = ImGui.GetBackgroundDrawList();
                            pMin = new Vector2(centre.X - halfWidth40,
                                centre.Y + centreMarkerOffset * scale - halfWidth40);
                            pMax = new Vector2(centre.X + halfWidth40,
                                centre.Y + centreMarkerOffset * scale + halfWidth40);
                            uv1            = flipCentreMarker ? new Vector2(1, -1) : Vector2.One;
                            playerPosition = new Vector2(mapIconComponentNode->AtkResNode.X, -mapIconComponentNode->AtkResNode.Y);
                            break;
                        //case  >5 and <9 :
                        //    break;
                        case 060495: // Small Area Circle
                        case 060496: // Big Area Circle
                        case 060497: // Another Circle
                        case 060498: // One More Circle
                            bool inArea;
                            (pMin, pMax, tintColour, inArea)
                                = Util.CalculateAreaCircleVariables(playerPosition, playerForward, mapIconComponentNode,
                                    imgNode, distanceOffset, compassUnit, halfWidth40, centre,
                                    maxDistance, minScaleFactor);
                            if (inArea)
                                //*((byte*) &tintColour + 3) = 0x33  == (0x33FFFFFF) & (tintColour)
                            {
                                ImGui.GetBackgroundDrawList().AddRectFilled(backgroundPMin
                                  , backgroundPMax
                                  , 0x33FFFFFF & tintColour //Set A to 0.2
                                  , backgroundRounding
                                );
                            }

                            break;
                        case 060541: // Arrow UP on Circle
                        case 060542: // Arrow UP on Circle
                        case 060543: // Another Arrow UP
                        case 060545: // Another Arrow DOWN
                        case 060546: // Arrow DOWN on Circle
                        case 060547: // Arrow DOWN on Circle
                            (pMin, pMax, tintColour, _)
                                = Util.CalculateAreaCircleVariables(playerPosition, playerForward, mapIconComponentNode,
                                    imgNode, distanceOffset, compassUnit, halfWidth40, centre,
                                    maxDistance, minScaleFactor);
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
                        case 060004: // The Hunt Mob Marker
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
                        case 060934: // FATE EXP Bonus Icon
                            if (mapIconComponentNode->AtkResNode.Rotation == 0)
                                // => The current quest marker is inside the mask and should be
                                // treated as a map point
                            {
                                goto default;
                            }

                            // => The current quest marker is outside the mask and should be
                            // treated as a rotation
                            goto case 1; //No UV setup needed for quest markers
                        default:
                            // NOTE (Chiv) Remember, Y needs to be flipped to transform to default coordinate system
                            var (distanceScaleFactor, iconAngle, _) = Util.CalculateDrawVariables(playerPosition,
                                new Vector2(
                                    mapIconComponentNode->AtkResNode.X,
                                    -mapIconComponentNode->AtkResNode.Y
                                ),
                                playerForward,
                                distanceOffset,
                                maxDistance,
                                minScaleFactor
                            );
                            var iconOffset    = compassUnit * iconAngle;
                            var iconHalfWidth = halfWidth40 * distanceScaleFactor;
                            pMin = new Vector2(centre.X - iconHalfWidth + iconOffset, centre.Y - iconHalfWidth);
                            pMax = new Vector2(centre.X + iconOffset + iconHalfWidth, centre.Y + iconHalfWidth);
                            break;
                    }

                    //PluginLog.Debug($"ID: {iconId}; Tintcolor: {tintColour:x8}");
                    if (rotate)
                    {
                        ImGuiHelper.ImageRotated(
                            textureIdPtr,
                            new Vector2(pMin.X + (pMax.X - pMin.X) * 0.5f, pMin.Y + (pMax.Y - pMin.Y) * 0.5f),
                            pMax - pMin,
                            imgNode->AtkResNode.Rotation,
                            uv,
                            uv1,
                            drawList
                        );
                    } else
                    {
                        drawList.AddImage(textureIdPtr, pMin, pMax, uv, uv1, tintColour);
                    }
                }
            }
        }
#if DEBUG
        catch (Exception e) {
            PluginLog.Error(e.ToString());
        }
#else
        catch
        {
            // ignored
        }
#endif
        return playerPosition;
    }
}