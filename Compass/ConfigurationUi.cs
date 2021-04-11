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

            if(ImGui.BeginTabBar("ConfigTabBar", ImGuiTabBarFlags.NoTooltip))
            {
                changed |= DrawConfigurationTab(config, scale);
                DrawHelpTab();
                ImGui.EndTabBar();
            }
            
            ImGui.End();
            return (shouldBuildConfigUi, changed);
        }

        private static void DrawHelpTab()
        {
            if (!ImGui.BeginTabItem("FAQ")) return;
            if (ImGui.TreeNode("How does this work?"))
            {
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("What are the current limitations?"))
            {
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("The icons are all placed wrong, what gives?"))
            {
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("How can I resize the compass?"))
            {
                ImGui.TreePop();
            }
            ImGui.EndTabItem();
        }

        private static bool DrawDummyTreeCombo(string label, ref bool open, Func<bool> drawConfig)
        {
            var changed = ImGui.Checkbox($"###{label.GetHashCode()}", ref open);
            ImGui.SameLine();
            if (open)
            {
                var x = ImGui.GetCursorPosX();
                if (!ImGui.TreeNodeEx(label, ImGuiTreeNodeFlags.DefaultOpen)) return changed;
                ImGui.SetCursorPosX(x);
                ImGui.BeginGroup();
                changed |= drawConfig();
                ImGui.EndGroup();
                ImGui.TreePop();
            }
            else
            {
                DummyTreeNode(label);
            }
            return changed;
        }

        private static bool DrawConfigurationTab(Configuration config, float scale)
        {
            if (!ImGui.BeginTabItem("Settings")) return false;
            var changed = false;
            //changed |= DrawNativeUITree(config, scale, changed);
            //ImGui.Separator();
            //changed = DrawImGuiTree(config, scale, changed);
            changed |= ImGui.Checkbox($"Enable Compass", ref config.ImGuiCompassEnable);
            if (config.ImGuiCompassEnable)
            {
                ImGui.Indent();
                ImGui.PushItemWidth(200f * scale);
                changed |= ImGui.DragFloat("Scale###ImGui", ref config.ImGuiCompassScale, 0.1f, 0.1f, 3f);
                changed |= ImGui.Checkbox("Disable Centre Marker##ImGui", ref config.ImGuiCompassDisableCenterMarker);
                if (!config.ImGuiCompassDisableCenterMarker)
                {
                    ImGui.Indent();
                    ImGui.SetNextItemWidth(35 * scale);
                    changed |= ImGui.DragInt("Centre Marker Offset##ImGui", ref config.ImGuiCompassCentreMarkerOffset);
                    changed |= ImGui.Checkbox("Flip Centre Marker##ImGui", ref config.ImGuiCompassFlipCentreMarker);
                    ImGui.Unindent();
                }
                changed |= ImGui.Checkbox("Disable Background##ImGui", ref config.ImGuiCompassDisableBackground);
                if (!config.ImGuiCompassDisableBackground)
                {
                    ImGui.Indent();
                    var backgroundStyle = (int) config.ImGuiCompassBackgroundStyle;
                    ImGui.Text("Background");
                    changed |= ImGui.RadioButton("Filled", ref backgroundStyle, 0);
                    ImGui.SameLine();
                    changed |= ImGui.RadioButton("Border", ref backgroundStyle, 1);
                    if (changed) config.ImGuiCompassBackgroundStyle = (ImGuiCompassBackgroundStyle) backgroundStyle;
                    changed |= ImGui.ColorEdit4("Background Colour##Edit", ref config.ImGuiBackgroundColor);
                    changed |= ImGui.DragFloat("Rounding", ref config.ImGuiCompassBackgroundRouding);
                    ImGui.Unindent();
                }
                ImGui.PopItemWidth();
                ImGui.Unindent();
            }

            ImGui.EndTabItem();
            return changed;

        }

        private static bool DrawImGuiTree(Configuration config, float scale, bool changed)
        {
            changed |= DrawDummyTreeCombo("Enable Compass", ref config.ImGuiCompassEnable, () =>
            {
                // ReSharper disable once VariableHidesOuterVariable
                changed = false;
                ImGui.PushItemWidth(200f * scale);
                changed |= ImGui.DragFloat("Scale###ImGui", ref config.ImGuiCompassScale,0.1f, 0.1f, 3f);
                changed |= ImGui.Checkbox("Disable Background##ImGui", ref config.ImGuiCompassDisableBackground);
                if (!config.ImGuiCompassDisableBackground)
                {
                    ImGui.Indent();
                    var backgroundStyle = (int) config.ImGuiCompassBackgroundStyle;
                    ImGui.Text("Background");
                    changed |= ImGui.RadioButton("Filled", ref backgroundStyle, 0);
                    ImGui.SameLine();
                    changed |= ImGui.RadioButton("Border", ref backgroundStyle, 1);
                    if (changed) config.ImGuiCompassBackgroundStyle = (ImGuiCompassBackgroundStyle) backgroundStyle;
                    changed |= ImGui.ColorEdit4("Background Colour##Edit", ref config.ImGuiBackgroundColor);
                    ImGui.Unindent();
                }

                ImGui.PopItemWidth();
                return changed;
            });
            return changed;
        }

        private static bool DrawNativeUITree(Configuration config, float scale, bool changed)
        {
            changed |= DrawDummyTreeCombo("Native UI Compass", ref config.AddonCompassEnable, () =>
            {
                // ReSharper disable once VariableHidesOuterVariable
                changed = false;
                ImGui.PushItemWidth(200f * scale);
                changed |= ImGui.DragFloat2("Position##Addon", ref config.AddonCompassOffset);
                changed |= ImGui.DragInt("Width##Addon", ref config.AddonCompassWidth);
                changed |= ImGui.DragFloat("Scale###Addon", ref config.AddonCompassScale);
                changed |= ImGui.Checkbox("Disable Background##Addon", ref config.AddonCompassDisableBackground);
                if (!config.AddonCompassDisableBackground)
                {
                    //ImGui.SetNextItemWidth(52* scale);
                    changed |= ImGui.SliderInt("##Addon", ref config.AddonCompassBackgroundPartId, 1, 23, "Background %d",
                        ImGuiSliderFlags.NoInput);
                }

                ImGui.PopItemWidth();
                return changed;
            });
            return changed;
        }

        private static void DummyTreeNode(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen)
        {
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
            ImGui.TreeNodeEx(label, flags);
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();
        }
    }
}