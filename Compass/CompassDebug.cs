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
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.GUI.ULD;
using ImGuiNET;
using SimpleTweaksPlugin;
using static Compass.Extensions;

namespace Compass
{
    public unsafe partial class Compass
    {
        const float TOLERANCE = 0.00001f;

        private delegate void AddonNaviMapOnUpdate(nint thisNaviMap, nint a2, nint a3);

        private Hook<AddonNaviMapOnUpdate> _naviOnUpdateHook;
        
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

        partial void DebugCtor(SigScanner sigScanner)
        {
            _fateSig = sigScanner.ScanText("80 3D ?? ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 48 8B 42 20");
            _inFateAreaPtr = _fateSig + Marshal.ReadInt32(_fateSig, 2) + 7;


            var naviOnUpdate =
                sigScanner.ScanText("48 8B C4 55 48 81 EC ?? ?? ?? ?? F6 81 ?? ?? ?? ?? ??");
            _naviOnUpdateHook =
                new Hook<AddonNaviMapOnUpdate>(naviOnUpdate, (AddonNaviMapOnUpdate) NaviMapOnUpdateDetour);
            _naviOnUpdateHook.Enable();

            _commands.AddHandler($"{Command}debug", new CommandInfo((_, _) =>
            {
                _pluginInterface.UiBuilder.Draw -= BuildDebugUi;
                _pluginInterface.UiBuilder.Draw += BuildDebugUi;
            })
            {
                HelpMessage = $"Open {PluginName} Debug menu.",
                ShowInHelp = false
            });
            if (_clientState.LocalPlayer is not null)
            {
                OnLogin(null!, null!);
            }
            
            _pluginInterface.UiBuilder.Draw += BuildDebugUi;

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

        partial void DebugDtor()
        {
            _pluginInterface.UiBuilder.Draw -= BuildDebugUi;
            _commands.RemoveHandler($"{Command}debug");
            
            _naviOnUpdateHook?.Disable();
            _naviOnUpdateHook?.Dispose();
        }
    }
}
#endif