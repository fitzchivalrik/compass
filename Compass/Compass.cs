using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Data.LuminaExtensions;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.Helper;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;
using static Compass.Extensions;

// TODO 5 Refactor DrawCombo to generic
namespace Compass
{
    public unsafe partial class Compass : IDisposable
    {
        public const string PluginName = "Compass";
        private const string Command = "/compass";

        private delegate void SetCameraRotationDelegate(nint cameraThis, float degree);
        private readonly Hook<SetCameraRotationDelegate> _setCameraRotation;
        private Configuration _config;
        private DalamudPluginInterface _pluginInterface;
        
        private nint _maybeCameraStruct;
        private int _currentUiObjectIndex;
        private bool _buildingConfigUi;
        private bool _isDisposed;
        private bool _shouldHideCompass;
        private bool _shouldHideCompassIteration;
        

        public Compass(DalamudPluginInterface pi, Configuration config)
        {
            #region Signatures

            const string setCameraRotationSignature = "40 ?? 48 83 EC ?? 0F 2F ?? ?? ?? ?? ?? 48 8B";

            #endregion


            _pluginInterface = pi;
            _config = config;

            #region Configuration Setup

            config.ShouldHideOnUiObject = new[]
            {
                  (new [] {"_BattleTalk"}, true, "Dialogue Box During Battle")
                , (new [] {"Talk"}, true, "Dialogue Box")
                , (new [] {"AreaMap"}, true, "Map")
                , (new [] {"Character"}, true, "Character")
                , (new [] {"ConfigCharacter"}, true, "Character Configuration")
                , (new [] {"ConfigSystem"}, false, "System Configuration")
                , (new [] {"Inventory", "InventoryLarge", "InventoryExpansion"}, true, "Inventory")
                , (new [] {"InventoryRetainer", "InventoryRetainerLarge"}, false, "Retainer Inventory")
                , (new [] {"InventoryBuddy"}, false, "Saddle Bag")
                , (new [] {"ArmouryBoard"}, false, "Armoury")
                , (new [] {"Shop", "InclusionShop", "ShopExchangeCurrency"}, true, "Shops")
                , (new [] {"Teleport"}, false, "Teleport")
                , (new [] {"ContentsInfo"}, false, "Timers")
                , (new [] {"ContentsFinder"}, false, "Duty")
                , (new [] {"LookingForGroup"}, false, "Party Finder")
                , (new [] {"AOZNotebook"}, false, "Bluemage Notebook")
                , (new [] {"MountNoteBook"}, false, "Mount Guide")
                , (new [] {"MinionNoteBook"}, false, "Minion Guide")
                , (new [] {"Achievement"}, false, "Achievements")
                , (new [] {"GoldSaucerInfo"}, false, "Action Help")
                , (new [] {"PvpProfile"}, false, "PVP Profile")
                , (new [] {"LinkShell"}, false, "Linkshell")
                , (new [] {"CrossWorldLinkshell"}, false, "Crossworld Linkshell")
                , (new [] {"ActionDetail"}, false, "Action Help (Tooltip)")
                , (new [] {"ItemDetail"}, false, "Item Tooltip")
                , (new [] {"ActionMenu"}, false, "Action List")
                , (new [] {"QuestRedo", "QuestRedoHud"}, true, "New Game+")
                , (new [] {"Journal"}, false, "Journal")
                , (new [] {"RecipeNote"}, true, "Crafting Log")
                , (new [] {"AdventureNoteBook"}, false, "Sightseeing Log")
                , (new [] {"GatheringNote"}, false, "Gathering Log")
                , (new [] {"FishingNote"}, false, "Fishing Log")
                , (new [] {"FishGuide"}, false, "Fishing Guide")
                , (new [] {"Orchestrion"}, false, "Orchestrion List")
                , (new [] {"ContentsNote"}, false, "Challenge Log")
                , (new [] {"MonsterNote"}, false, "Hunting Log")
                , (new [] {"PartyMemberList"}, false, "Party Members")
                , (new [] {"FriendList"}, false, "Friend list")
                , (new [] {"BlackList"}, false, "Black List")
                , (new [] {"SocialList"}, true, "Player Search")
                , (new [] {"Emote"}, false, "Emote")
                , (new [] {"FreeCompany"}, false, "Free Company")
                , (new [] {"SupportDesk"}, true, "Support Desk")
                , (new [] {"ConfigKeybind"}, true, "Keybinds")
                , (new [] {"_HudLayoutScreen"}, true, "HUD Manager")
                , (new [] {"Macro"}, true, "Macro")
                , (new [] {"GrandCompanySupplyList"}, false, "Grand Company Delivery")
                , (new [] {"GrandCompanyExchange"}, false, "Grand Company Shop")
                , (new [] {"MiragePrismPrismBox"}, true, "Glamour Dresser")
                , (new [] {"Currency"}, true, "Currency")
                , (new [] {"_MainCross"}, true, "Controller Main Menu")
            };

            
            for (var i = 0; i < config.ShouldHideOnUiObjectSerializer.Length; i++)
            {
                config.ShouldHideOnUiObject[i].disable = config.ShouldHideOnUiObjectSerializer[i];
            }

            if (config.ShouldHideOnUiObjectSerializer.Length < config.ShouldHideOnUiObject.Length)
            {
                Array.Resize(ref config.ShouldHideOnUiObjectSerializer, config.ShouldHideOnUiObject.Length);
            }
        
            #endregion
            
            _pluginInterface.ClientState.OnLogin += OnLogin;
            _pluginInterface.ClientState.OnLogout += OnLogout;

            #region Hooks, Functions and Addresses
            
            _setCameraRotation = new Hook<SetCameraRotationDelegate>(
                _pluginInterface.TargetModuleScanner.ScanText(setCameraRotationSignature),
                (SetCameraRotationDelegate) SetCameraRotationDetour);
            _setCameraRotation.Enable();
            
            #endregion

            #region Excel Data

            #endregion
            
            pi.CommandManager.AddHandler(Command, new CommandInfo((_, _) => { OnOpenConfigUi(null!, null!); })
            {
                HelpMessage = $"Open {PluginName} configuration menu.",
                ShowInHelp = true
            });

#if DEBUG
            DebugCtor();
#else

            if (_pluginInterface.Reason == PluginLoadReason.Installer
                   || _pluginInterface.ClientState.LocalPlayer is not null
            )
                OnLogin(null!, null!);
#endif
        }

        private void SetCameraRotationDetour(nint cameraThis, float degree)
        {
            _maybeCameraStruct = cameraThis;
            _setCameraRotation.Original(cameraThis, degree);
            _setCameraRotation.Disable();
        }
        

        private void OnLogout(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi -= OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi -= BuildUi;
        }

        private void OnLogin(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi += OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi += BuildUi;
        }

        private void BuildImGuiCompass()
        {
            UpdateHideCompass();
            if (_shouldHideCompass) return;
            if (!_config.ImGuiCompassEnable) return;
            var naviMapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (naviMapPtr == IntPtr.Zero) return;
            var naviMap = (AtkUnitBase*) naviMapPtr;
            //NOTE (chiv) 3 means fully loaded
            if (naviMap->ULDData.LoadedState != 3) return;
            // TODO (chiv) Check the flag if _NaviMap is hidden in the HUD
            if (!naviMap->IsVisible) return;
            var scale = _config.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
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
            const int playerX = 72, playerY = 72;
            const uint whiteColor = 0xFFFFFFFF;
            var cameraRotationInRadian = -*(float*) (_maybeCameraStruct + 0x130);
            //SimpleLog.Log($"{-cameraRotationInRadian}");
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
            if (_config.ImGuiCompassEnableBackground)
            {
                if (_config.ImGuiCompassFillBackground)
                    backgroundDrawList.AddRectFilled(
                        backgroundPMin
                        , backgroundPMax
                        , ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundColour)
                        , _config.ImGuiCompassBackgroundRounding
                    );
                if (_config.ImGuiCompassDrawBorder)
                    backgroundDrawList.AddRect(
                        backgroundPMin - Vector2.One
                        , backgroundPMax + Vector2.One
                        , ImGui.ColorConvertFloat4ToU32(_config.ImGuiBackgroundBorderColour)
                        , _config.ImGuiCompassBackgroundRounding
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
                        var textureFileName = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr))!;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (Vector2 pMin, Vector2 pMax, uint tintColour, bool inArea) CalculateAreaCirlceVariables(
            Vector2 playerPos, Vector2 playerForward, AtkComponentNode* mapIconComponentNode,
            AtkImageNode* imgNode, float mapScale, float compassUnit, float halfWidth32, Vector2 compassCentre)
        {
            // TODO Distinguish between Circles for quests and circles for Fates (colour?)
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
            // NOTE (Chiv) We asumme part.Width == part.Height == 32
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

        //TODO Extract to Extensions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (float scaleFactor, float signedAngle, float distance) CalculateDrawVariables(Vector2 from,
            Vector2 to, Vector2 forward, float distanceScaling)
        {
            const float lowestScaleFactor = 0.3f;
            var distanceOffset = 40f * distanceScaling; //80f @Max Zoom(==2) _NaviMap
            var maxDistance = 180f * distanceScaling; //360f @Max Zoom(==2) _NaviMap
            //TODO (Chiv) Oh boy, check the math
            var distance = Vector2.Distance(to, from);
            var scaleFactor = Math.Max(1f - (distance - distanceOffset) / maxDistance, lowestScaleFactor);
            //return (scaleFactor,SignedAngle(to  - from, forward), distance);
            return (scaleFactor, SignedAngle(from - to, forward), distance);
        }

        //TODO Extract to Extensions
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
        
        private void UpdateHideCompass()
        {
            for (var i = 0; i < 8; i++)
            {
                var (uiIdentifiers, disable, _) = _config.ShouldHideOnUiObject[_currentUiObjectIndex++];
                _currentUiObjectIndex %= _config.ShouldHideOnUiObject.Length;
                if (_currentUiObjectIndex == 0)
                {
                    if (_config.HideInCombat)
                    {
                        _shouldHideCompassIteration |= _pluginInterface.ClientState.Condition[ConditionFlag.InCombat];
                    }
                    _shouldHideCompass = _shouldHideCompassIteration;
                    _shouldHideCompassIteration = false;
                }
                if (!disable) continue;
                foreach (var identifier in uiIdentifiers)
                {
                    var unitBase = _pluginInterface.Framework.Gui.GetUiObjectByName(identifier, 1);
                    if (unitBase == IntPtr.Zero) continue;
                    _shouldHideCompassIteration |= ((AtkUnitBase*) unitBase)->IsVisible;
                }                
            }
        }

       

       

        private static AtkResNode* CloneAndAttach(ref ULDData uld, AtkResNode* parent, AtkResNode* prototype,
            uint nodeId = uint.MaxValue)
        {
            var clone = UiHelper.CloneNode(prototype);
            clone->NodeID = nodeId;
            clone->ParentNode = parent;
            var currentLastSibling = parent->ChildNode;
            while (currentLastSibling->PrevSiblingNode != null)
                currentLastSibling = currentLastSibling->PrevSiblingNode;
            clone->PrevSiblingNode = null;
            clone->NextSiblingNode = currentLastSibling;
            currentLastSibling->PrevSiblingNode = clone;
            uld.NodeList[uld.NodeListCount++] = clone;
            return clone;
        }

        #region UI

        private void BuildUi()
        {
            BuildImGuiCompass();
            if (!_buildingConfigUi) return;
            var (shouldBuildConfigUi, changedConfig) = ConfigurationUi.DrawConfigUi(_config);
            if (changedConfig)
                _pluginInterface.SavePluginConfig(_config);

            if (!shouldBuildConfigUi) _buildingConfigUi = false;
        }


        private void OnOpenConfigUi(object sender, EventArgs e)
        {
            _buildingConfigUi = !_buildingConfigUi;
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                // TODO (Chiv) Still not quite sure about correct dispose
                // NOTE (Chiv) Explicit, non GC? call - remove managed thingies too.
                OnLogout(null!, null!);
                _pluginInterface.ClientState.OnLogin -= OnLogin;
                _pluginInterface.ClientState.OnLogout -= OnLogout;
                _pluginInterface.CommandManager.RemoveHandler(Command);

                _setCameraRotation?.Disable();
                _setCameraRotation?.Dispose();
                
#if DEBUG
                _pluginInterface.UiBuilder.OnBuildUi -= BuildDebugUi;
                _pluginInterface.CommandManager.RemoveHandler($"{Command}debug");
#endif
            }

            _isDisposed = true;
        }

        ~Compass()
        {
            Dispose(false);
        }

        #endregion
    }
}