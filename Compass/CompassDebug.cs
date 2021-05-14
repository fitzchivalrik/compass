#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.Helper;
using static Compass.Extensions;

namespace Compass
{
    public unsafe partial class Compass
    {
        const float TOLERANCE = 0.00001f;

        private delegate void AddonNaviMap_OnUpdate(nint thisNaviMap, nint a2, nint a3);

        private Hook<AddonNaviMap_OnUpdate> _naviOnUpdateHook;
        
        private AtkImageNode* _background;
        //NOTE (Chiv) 202 Component Nodes for the Icons on the minimap + 1*4 pointers for the Cardinals
        // Actually, there are 101*3 and 101*4 Component nodes but this it easier to deal with 
        private readonly AtkImageNode*[,] _clonedImageNodes = new AtkImageNode*[202, 4];
        private readonly AtkImageNode*[] _cardinalsClonedImageNodes = new AtkImageNode*[4];
        private bool _setupComplete = false;
        private IntPtr _inFateAreaPtr;
        private IntPtr _fateSig;
        private nint _thisNaviMap;
        private nint _a2NaviOnUpdate;
        private nint _a3NaviOnUpdate;

        partial void DebugCtor()
        {
            _fateSig = _pluginInterface.TargetModuleScanner.ScanText("80 3D ?? ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 48 8B 42 20");
            _inFateAreaPtr = _fateSig + Marshal.ReadInt32(_fateSig, 2) + 7;


            var naviOnUpdate =
                _pluginInterface.TargetModuleScanner.ScanText("48 8B C4 55 48 81 EC ?? ?? ?? ?? F6 81 ?? ?? ?? ?? ??");
            _naviOnUpdateHook =
                new Hook<AddonNaviMap_OnUpdate>(naviOnUpdate, (AddonNaviMap_OnUpdate) NaviMapOnUpdateDetour);
            _naviOnUpdateHook.Enable();

            _pluginInterface.CommandManager.AddHandler($"{Command}debug", new CommandInfo((_, _) =>
            {
                _pluginInterface.UiBuilder.OnBuildUi -= BuildDebugUi;
                _pluginInterface.UiBuilder.OnBuildUi += BuildDebugUi;
            })
            {
                HelpMessage = $"Open {PluginName} Debug menu.",
                ShowInHelp = false
            });
            if (_pluginInterface.ClientState.LocalPlayer is not null)
            {
                OnLogin(null!, null!);
            }
            
            _pluginInterface.UiBuilder.OnBuildUi += BuildDebugUi;

            UiHelper.Setup(_pluginInterface.TargetModuleScanner);
        }

        private void NaviMapOnUpdateDetour(nint thisNaviMap, nint a2, nint a3)
        {
            _thisNaviMap = thisNaviMap;
            _a2NaviOnUpdate = a2;
            _a3NaviOnUpdate = a3;
            _naviOnUpdateHook.Original(thisNaviMap, a2, a3);
            if (_setupComplete)
            {
                //OnFrameworkUpdateUpdateAddonCompass();
            }
            else
            {
                //OnFrameworkUpdateSetupAddonNodes();
            }
        }
       
        private void BuildDebugUi()
        {
            
            ImGui.SetNextWindowBgAlpha(1);
            if(!ImGui.Begin($"{PluginName} Debug")) { ImGui.End(); return;}
            ImGui.Text($"Sig: {_fateSig.ToString("X")}, Sig Offset 2 Int {Marshal.ReadInt32(_fateSig,2):X} Sig Offset 2 Int+7 {Marshal.ReadInt32(_fateSig,2)+7:X}");
            ImGui.Text($"Is in Fate {Marshal.ReadByte(_inFateAreaPtr)}, Ptr {_inFateAreaPtr.ToString("X")}");
            ImGui.Separator();
            ImGui.Text($"thisNaviMap {(long)_thisNaviMap:X} a2 {(long)_a2NaviOnUpdate:X} a3 {(long)_a3NaviOnUpdate:X}");
            ImGui.Separator();
            //var naviMap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            //var areaMap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("AreaMap", 1);
            //ImGui.Text($"LoadedState of _NaviMap {naviMap->ULDData.LoadedState}");
            //ImGui.Text($"LoadedState of AreaMap {areaMap->ULDData.LoadedState}");

           // ImGui.Separator();
           
            
            ImGui.End();
        }
        
         //TODO (Chiv) While this is nice and somehow works, it still explodes 1-30 seconds later, so I give up on that for now
        private void OnFrameworkUpdateUpdateAddonCompass()
        {
            // Minimap rotation thingy is even already flipped!
            // And apparently even accessible & updated if _NaviMap is disabled

            var navimapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            const uint playerViewTriangleRotationOffset = 0x254;
            if (navimapPtr == IntPtr.Zero) return;
            var naviMap = (AtkUnitBase*) navimapPtr;
            //NOTE (chiv) 3 means fully loaded
            if (naviMap->ULDData.LoadedState != 3) return;
            if (!naviMap->IsVisible) return;
            var cameraRotationInRadian = *(float*)((nint)naviMap + playerViewTriangleRotationOffset) * Deg2Rad;

            try
            {
                // NOTE (Chiv) This is the position of the player in the minimap coordinate system
                const int playerX = 72, playerY = -72;
                const float distanceOffset = 80f;
                const float maxDistance = 350f;
                const float lowestScaleFactor = 0.3f;
                var playerPos = new Vector2(playerX, playerY);
                var scale = 1f / naviMap->Scale * _config.AddonCompassScale;
                var scaleFactorForRotationBasedDistance =
                    Math.Max(1f - distanceOffset / maxDistance, lowestScaleFactor);
                var playerCos = (float) Math.Cos(cameraRotationInRadian);
                var playerSin = (float) Math.Sin(cameraRotationInRadian);
                var playerViewVector = new Vector2(-playerSin, playerCos);
                var compassUnit = _config.AddonCompassWidth / (2f*(float)Math.PI);
                //First, the background
                UiHelper.SetSize(_background, _config.AddonCompassWidth, 50);
                _background->PartId = (ushort) _config.AddonCompassBackgroundPartId;
                UiHelper.SetPosition(
                    _background,
                    _config.AddonCompassOffset.X - _config.AddonCompassWidth / 2f,
                    _config.AddonCompassOffset.Y);
                _background->AtkResNode.ScaleX = _background->AtkResNode.ScaleY = scale;
                UiHelper.SetVisible((AtkResNode*) _background, !_config.AddonCompassDisableBackground);
                // Second, we position our Cardinals
                var east = Vector2.UnitX;
                var south = -Vector2.UnitY;
                var west = -Vector2.UnitX;
                var north = Vector2.UnitY;
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[0],
                    _config.AddonCompassOffset.X + compassUnit * SignedAngle(east, playerViewVector),
                    _config.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[1],
                    _config.AddonCompassOffset.X + compassUnit * SignedAngle(south, playerViewVector),
                    _config.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[2],
                    _config.AddonCompassOffset.X + compassUnit * SignedAngle(west, playerViewVector),
                    _config.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[3],
                    _config.AddonCompassOffset.X + compassUnit * SignedAngle(north, playerViewVector),
                    _config.AddonCompassOffset.Y);

                _cardinalsClonedImageNodes[0]->AtkResNode.ScaleX =
                    _cardinalsClonedImageNodes[0]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[1]->AtkResNode.ScaleX =
                    _cardinalsClonedImageNodes[1]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[2]->AtkResNode.ScaleX =
                    _cardinalsClonedImageNodes[2]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[3]->AtkResNode.ScaleX =
                    _cardinalsClonedImageNodes[3]->AtkResNode.ScaleY = scale;

                //Then, we do the dance through all relevant nodes on _NaviMap
                var iconsRootComponentNode = (AtkComponentNode*) naviMap->ULDData.NodeList[2];
                for (var i = 4; i < iconsRootComponentNode->Component->ULDData.NodeListCount; i++)
                {
                    var iconComponentNode =
                        (AtkComponentNode*) iconsRootComponentNode->Component->ULDData.NodeList[i];
                    for (var j = 2; j < iconComponentNode->Component->ULDData.NodeListCount; j++)
                    {
                        // NOTE (Chiv) Invariant: From 2 onward, only ImageNodes
                        var clone = _clonedImageNodes[i - 4, j - 2];
                        var imgNode = (AtkImageNode*) iconComponentNode->Component->ULDData.NodeList[j];
                        if (imgNode->AtkResNode.Type != NodeType.Image)
                        {
                            SimpleLog.Error($"{i}{j} was not an ImageNode");
                            continue;
                        }

                        clone->PartId = 0;

                        var show = iconComponentNode->AtkResNode.IsVisible && imgNode->AtkResNode.IsVisible;
                        if (!show)
                        {
                            // NOTE (Chiv) Shown + PartsList null explodes
                            UiHelper.Hide(clone);
                            clone->PartsList = null;
                            continue;
                        }

                        ;
                        clone->PartsList = imgNode->PartsList;
                        clone->PartId = imgNode->PartId;
                        clone->WrapMode = imgNode->WrapMode;
                        clone->AtkResNode.Width = imgNode->AtkResNode.Width;
                        clone->AtkResNode.Height = imgNode->AtkResNode.Height;
                        clone->AtkResNode.AddBlue = imgNode->AtkResNode.AddBlue;
                        clone->AtkResNode.AddGreen = imgNode->AtkResNode.AddGreen;
                        clone->AtkResNode.AddRed = imgNode->AtkResNode.AddRed;
                        clone->AtkResNode.MultiplyBlue = imgNode->AtkResNode.MultiplyBlue;
                        clone->AtkResNode.MultiplyGreen = imgNode->AtkResNode.MultiplyGreen;
                        clone->AtkResNode.MultiplyRed = imgNode->AtkResNode.MultiplyRed;
                        clone->AtkResNode.Color = imgNode->AtkResNode.Color;
                        clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale;
                        UiHelper.Show(clone);
                        var part = imgNode->PartsList->Parts[imgNode->PartId];
                        var type = part.UldAsset->AtkTexture.TextureType;
                        // OR?? //NOTE (CHIV) It should always be a resource
                        if (type != TextureType.Resource)
                        {
                            SimpleLog.Error($"{i}{j} was not a Resource Texture");
                            continue;
                        }

                        ;
                        var texFileNamePtr =
                            part.UldAsset->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                        switch (texString)
                        {
                            case var _ when texString?.EndsWith("060443.tex",
                                StringComparison.InvariantCultureIgnoreCase) ?? false: //Player Marker
                                UiHelper.SetPosition(
                                    clone,
                                    _config.AddonCompassOffset.X,
                                    -_config.AddonCompassOffset.Y);
                                break;
                            case var _ when texString?.EndsWith("060457.tex",
                                StringComparison.InvariantCultureIgnoreCase) ?? false: // Area Transition Bullet Thingy
                                var toObject = new Vector2(playerX - iconComponentNode->AtkResNode.X,
                                    playerY - -iconComponentNode->AtkResNode.Y);
                                var distanceToObject = Vector2.Distance(
                                    new Vector2(iconComponentNode->AtkResNode.X, -iconComponentNode->AtkResNode.Y),
                                    playerPos) - distanceOffset;
                                var scaleFactor = Math.Max(1f - distanceToObject / maxDistance, lowestScaleFactor);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale * scaleFactor;
                                UiHelper.SetPosition(
                                    clone,
                                    _config.AddonCompassOffset.X +
                                    compassUnit * SignedAngle(toObject, playerViewVector),
                                    -_config.AddonCompassOffset.Y);
                                break;
                            case var _ when (texString?.EndsWith("NaviMap.tex",
                                StringComparison.InvariantCultureIgnoreCase) ?? false) && imgNode->PartId == 21:
                                UiHelper.Hide(clone);
                                break;
                            case var _ when iconComponentNode->AtkResNode.Rotation == 0:
                                var toObject2 = new Vector2(playerX - iconComponentNode->AtkResNode.X,
                                    playerY - -iconComponentNode->AtkResNode.Y);
                                var distanceToObject2 = Vector2.Distance(
                                    new Vector2(iconComponentNode->AtkResNode.X, -iconComponentNode->AtkResNode.Y),
                                    playerPos) - distanceOffset;
                                var scaleFactor2 = Math.Max(1f - distanceToObject2 / maxDistance, lowestScaleFactor);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale * scaleFactor2;
                                UiHelper.SetPosition(
                                    clone,
                                    _config.AddonCompassOffset.X +
                                    compassUnit * SignedAngle(toObject2, playerViewVector),
                                    _config.AddonCompassOffset.Y);
                                break;
                            default:
                                var cosArrow = (float) Math.Cos(iconComponentNode->AtkResNode.Rotation);
                                var sinArrow = (float) Math.Sin(iconComponentNode->AtkResNode.Rotation);
                                var toObject3 = new Vector2(sinArrow, cosArrow);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY =
                                    scale * scaleFactorForRotationBasedDistance;
                                UiHelper.SetPosition(
                                    clone,
                                    _config.AddonCompassOffset.X +
                                    compassUnit * SignedAngle(toObject3, playerViewVector),
                                    _config.AddonCompassOffset.Y);
                                break;
                        }
                    }
                }

                //TODO (Chiv) Later, we might do that for AreaMap too
            }
            catch
            {
                // ignored
            }
        }
        
         //TODO (Chiv) See comment on OnFrameworkUpdateUpdateAddonCompass
        private void OnFrameworkUpdateSetupAddonNodes()
        {
            try
            {
                var navimapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
                if (navimapPtr == IntPtr.Zero) return;
                var naviMap = (AtkUnitBase*) navimapPtr;
                //NOTE (chiv) 3 means fully loaded
                if (naviMap->ULDData.LoadedState != 3) return;
                //NOTE (chiv) There should be no real need to care for visibility for cloning but oh well, untested.
                if (!naviMap->IsVisible) return;
                //NOTE (chiv) 19 should be the unmodified default
                //if (naviMap->ULDData.NodeListCount != 19) return;

                UiHelper.ExpandNodeList(naviMap, (ushort) _clonedImageNodes.Length);
                var prototypeImgNode = naviMap->ULDData.NodeList[11];
                var currentNodeId = uint.MaxValue;

                //First, Background (Reusing empty slot)
                _background = (AtkImageNode*) CloneAndAttach(
                    ref naviMap->ULDData,
                    naviMap->RootNode,
                    prototypeImgNode,
                    currentNodeId--);
                _background->WrapMode = 2;
                if (_config.AddonCompassDisableBackground) UiHelper.Hide(_background);

                // Next, Cardinals
                for (var i = 0; i < _cardinalsClonedImageNodes.Length; i++)
                    _cardinalsClonedImageNodes[i] = (AtkImageNode*) CloneAndAttach(
                        ref naviMap->ULDData,
                        naviMap->RootNode,
                        naviMap->ULDData.NodeList[i + 9],
                        currentNodeId--);

                // NOTE (Chiv) Set for rest
                // This is overkill but we are basically mimicking the amount of nodes in _NaviMap.
                // It could be that all are used!
                for (var i = 0; i < _clonedImageNodes.GetLength(0); i++)
                for (var j = 0; j < _clonedImageNodes.GetLength(1); j++)
                {
                    _clonedImageNodes[i, j] = (AtkImageNode*) CloneAndAttach(
                        ref naviMap->ULDData,
                        naviMap->RootNode,
                        prototypeImgNode,
                        currentNodeId--);
                    // Explodes if PartsList = null before Hiding it
                    UiHelper.Hide(_clonedImageNodes[i, j]);
                    _clonedImageNodes[i, j]->PartId = 0;
                    _clonedImageNodes[i, j]->PartsList = null;
                }

                //TODO (Chiv) Search for PlayerIcon and set it up too?
                _setupComplete = true;
                //_pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdateSetupAddonNodes;
                // _pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdateUpdateAddonCompass;
            }
            catch (Exception e)
            {
                SimpleLog.Error(e);
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

        partial void DebugDtor()
        {
            _pluginInterface.UiBuilder.OnBuildUi -= BuildDebugUi;
            _pluginInterface.CommandManager.RemoveHandler($"{Command}debug");
            
            _naviOnUpdateHook?.Disable();
            _naviOnUpdateHook?.Dispose();
        }
    }
}
#endif