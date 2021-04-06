using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Configuration;
using Dalamud.Interface;
using Dalamud.Plugin;
using Compass.Interop;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace Compass
{
    internal static class ConfigurationUi
    {
        internal static (bool shouldBuildConfigUi,bool changedConfig) DrawConfigUi(Configuration config)
        {
            var shouldBuildConfigUi = true;
            var changed = false;
            var scale = ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSize(new Vector2(575 * scale, 400 * scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale),
                new Vector2(float.MaxValue, float.MaxValue));
            if (!ImGui.Begin($"{Compass.PluginName} Configuration", ref shouldBuildConfigUi,
                ImGuiWindowFlags.NoCollapse))
            {
                ImGui.End();
                return (shouldBuildConfigUi, changed);
            }

            ImGui.Text("Hello World from ConfigurationUI!");
            ImGui.End();
            return (shouldBuildConfigUi, changed);
        }

        
    }
}