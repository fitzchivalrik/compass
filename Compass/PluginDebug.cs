#if DEBUG
using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace Compass;

public unsafe partial class Plugin {
    partial void DebugCtor(SigScanner sigScanner) {
        _commands.AddHandler($"{Command}debug", new CommandInfo((_, _) => {
            _pluginInterface.UiBuilder.Draw -= BuildDebugUi;
            _pluginInterface.UiBuilder.Draw += BuildDebugUi;
        }) {
            HelpMessage = $"Open {PluginName} Debug menu.",
            ShowInHelp = false
        });


        _pluginInterface.UiBuilder.Draw += BuildDebugUi;
    }


    private void BuildDebugUi() {
        ImGui.SetNextWindowBgAlpha(1);
        if (!ImGui.Begin($"{PluginName} Debug")) {
            ImGui.End();
            return;
        }

        ImGui.Text($"TargetSystem Pointer {(long)_compass._pointers.TargetSystem:x16}");


        ImGui.End();
    }

    partial void DebugDtor() {
        _pluginInterface.UiBuilder.Draw -= BuildDebugUi;
        _commands.RemoveHandler($"{Command}debug");
    }
}
#endif