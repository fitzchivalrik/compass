using System;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
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
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowSize(new Vector2(400 * scale, 410 * scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(410 * scale, 200 * scale),
                new Vector2(float.MaxValue, float.MaxValue));
            if (!ImGui.Begin($"{Compass.PluginName} Configuration", ref shouldBuildConfigUi,
                ImGuiWindowFlags.NoCollapse))
            {
                ImGui.End();
                return (shouldBuildConfigUi, changed);
            }
            
            if (config.FreshInstall)
            {
                DrawFreshInstallNotice(config, scale);
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

        private static void DrawFreshInstallNotice(Configuration config, float scale)
        {
            ImGui.OpenPopup("Compass Note");
            var contentSize = ImGuiHelpers.MainViewport.Size;
            var modalSize = new Vector2(410 * scale, 195 * scale);
            var modalPosition = new Vector2(contentSize.X / 2 - modalSize.X / 2, contentSize.Y / 2 - modalSize.Y / 2);
            ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
            if (!ImGui.BeginPopupModal("Compass Note")) return;
            ImGui.Text("Thank you for installing the Compass plugin.");
            ImGui.Text("The compass should be in upper left corner of the game window.");
            ImGui.Text("Please have a look at the configuration, especially at the FAQ section,");
            ImGui.Text("for a quick explanation of the current caveats and settings needed");
            ImGui.Text("for the compass to work.");
            ImGui.Text("May Hydaelyn protect you in your exploration of Eorzea and beyond!");
            ImGui.Spacing();
            if (ImGui.Button("Continue the adventure##FreshInstallPopUp"))
            {
                config.FreshInstall = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();

        }

        private static void DrawHelpTab()
        {
            if (!ImGui.BeginTabItem("FAQ")) return;
            if (ImGui.TreeNode("How does this work?"))
            {
                ImGui.TextWrapped($"The current implementation reads the mini map data and");
                ImGui.Text($"displays it on the monodimensional compass.");
                ImGui.Text($"This also works when the mini map HUD element is hidden. ");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("What are the current limitations?"))
            {
                ImGui.TextWrapped($"Because of the current implementation,");
                ImGui.Text($"only data the mini map knows about can be displayed.");
                ImGui.Text($"This usually means icons in the vicinity of around 200 units");
                ImGui.Text($"of the player plus object markers for the current tracked quests.");
                ImGui.Text($"Different zoom levels of the mini map influence not the overall");
                ImGui.Text($"distance but how soon the 'arrow' icon for tracked quests");
                ImGui.Text($"turn into a more proper goal icon (e.g. area circle).");
                ImGui.Text($"I therefore would recommend zooming out as much as possible");
                ImGui.Text($"but this is entirely up to preference.");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("The icons are all placed wrong, what gives?"))
            {
                ImGui.TextWrapped("The current math only supports");
                ImGui.Text("a mini map which is locked  (meaning north is always upwards).");
                ImGui.Text("The mini map can be locked with a click on the cog");
                ImGui.Text($"on the mini map HUD element.");
                ImGui.Text("(In the case of Material UI, its a lock, not a cog.)");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("How can I resize/move the compass?"))
            {
                ImGui.TextWrapped($"The icons on the compass can be resized in this settings menu");
                ImGui.Text($"(other tab), the overall size of the compass (e.g. the height) is");
                ImGui.Text($"influenced by the global font scale set in the Dalamud settings.");
                ImGui.Text($"As long as the configuration window is open,");
                ImGui.Text($"the compass width and position can also be adjusted.");
                ImGui.TreePop();
            }
            ImGui.EndTabItem();
        }

        private static bool DrawTreeCheckbox(string label, ref bool open, Func<bool> drawConfig)
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
                changed |= ImGui.DragFloat("Scale##ImGui", ref config.ImGuiCompassScale, 0.1f, 0.1f, 3f);
                changed |= DrawTreeCheckbox("Enable Centre Marker", ref config.ImGuiCompassEnableCenterMarker, () =>
                    {
                        var changed = false;
                        ImGui.SetNextItemWidth(35 * scale);
                        changed |= ImGui.DragInt("Centre Marker Offset##ImGui",
                            ref config.ImGuiCompassCentreMarkerOffset);
                        changed |= ImGui.Checkbox("Flip Centre Marker##ImGui", ref config.ImGuiCompassFlipCentreMarker);
                        return changed;
                    });
                changed |= DrawTreeCheckbox("Enable Background", ref config.ImGuiCompassEnableBackground, () =>
                {
                    var changed = false;
                    var backgroundStyle = config.ImGuiCompassDrawBorder 
                        ? config.ImGuiCompassFillBackground
                            ? 2
                            : 0
                        : 1;
                    ImGui.Text($"Background");
                    changed |= ImGui.RadioButton("Border##ImGui", ref backgroundStyle, 0);
                    ImGui.SameLine();
                    changed |= ImGui.RadioButton("Filled##ImGui", ref backgroundStyle, 1);
                    ImGui.SameLine();
                    changed |= ImGui.RadioButton("Both##ImGui", ref backgroundStyle, 2);
                    if (changed)
                    {
                        config.ImGuiCompassDrawBorder = backgroundStyle == 0 || backgroundStyle == 2;
                        config.ImGuiCompassFillBackground = backgroundStyle == 1 || backgroundStyle == 2;
                    }
                    if(config.ImGuiCompassFillBackground)
                        changed |= ImGui.ColorEdit4("Background Colour##ImGui", ref config.ImGuiBackgroundColour);
                    if(config.ImGuiCompassDrawBorder)
                        changed |= ImGui.ColorEdit4("Background Border Colour##ImGui", ref config.ImGuiBackgroundBorderColour);
                    changed |= ImGui.DragFloat("Rounding##ImGui", ref config.ImGuiCompassBackgroundRounding);
                    return changed;
                });
                changed |= ImGui.Checkbox("Hide Compass when in Combat##ImGui", ref config.HideInCombat);
                if (ImGui.TreeNodeEx("Hide Compass when ... window is open."))
                {
                    for (var i = 0; i < config.ShouldHideOnUiObject.Length; i++)
                    {
                        changed |= ImGui.Checkbox(
                            config.ShouldHideOnUiObject[i].userFacingIdentifier
                            , ref config.ShouldHideOnUiObject[i].disable);
                        if (changed)
                        {
                            config.ShouldHideOnUiObjectSerializer[i] = config.ShouldHideOnUiObject[i].disable;
                        }
                    }
                    ImGui.TreePop();
                }
                ImGui.PopItemWidth();
                ImGui.Unindent();
            }

            ImGui.EndTabItem();
            return changed;

        }

        private static bool DrawImGuiTree(Configuration config, float scale, bool changed)
        {
            changed |= DrawTreeCheckbox("Enable Compass", ref config.ImGuiCompassEnable, () =>
            {
                // ReSharper disable once VariableHidesOuterVariable
                changed = false;
                ImGui.PushItemWidth(200f * scale);
                changed |= ImGui.DragFloat("Scale###ImGui", ref config.ImGuiCompassScale,0.1f, 0.1f, 3f);
                changed |= ImGui.Checkbox("Disable Background##ImGui", ref config.ImGuiCompassEnableBackground);
                if (config.ImGuiCompassEnableBackground)
                {
                    ImGui.Indent();
                  
                    ImGui.Unindent();
                }

                ImGui.PopItemWidth();
                return changed;
            });
            return changed;
        }

        private static bool DrawNativeUITree(Configuration config, float scale, bool changed)
        {
            changed |= DrawTreeCheckbox("Native UI Compass", ref config.AddonCompassEnable, () =>
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