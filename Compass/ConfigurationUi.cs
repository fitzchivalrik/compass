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
                changed |= DrawGeneralConfigurationTab(config, scale);
                changed |= DrawOnAddonHideTab(config, scale);
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
            var modalSize = new Vector2(418 * scale, 175 * scale);
            var modalPosition = new Vector2(contentSize.X / 2 - modalSize.X / 2, contentSize.Y / 2 - modalSize.Y / 2);
            ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
            if (!ImGui.BeginPopupModal("Compass Note")) return;
            ImGui.Text("Thank you for installing the Compass plugin.");
            ImGui.Text("Please have a look at the configuration, especially at the FAQ section,");
            ImGui.Text("for a quick explanation of the current caveats and settings needed");
            ImGui.Text("for the compass to work.");
            ImGui.Text("May Hydaelyn protect you in your adventures across Eorzea and beyond!");
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
                ImGui.TextWrapped($"The current implementation reads the minimap data and");
                ImGui.Text($"displays it on the monodimensional compass.");
                ImGui.Text($"This also works when the minimap HUD element is hidden. ");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("What are the current limitations?"))
            {
                ImGui.TextWrapped($"Because of the current implementation,");
                ImGui.Text($"only data the minimap knows about can be displayed.");
                ImGui.Text($"This usually means icons immediate vicinity");
                ImGui.Text($"of the player plus object markers for the current tracked quests.");
                ImGui.Text($"Different zoom levels of the minimap influence not the overall");
                ImGui.Text($"distance but how soon the 'arrow' icon for tracked quests");
                ImGui.Text($"turns into a more proper goal icon (e.g. area circle).");
                ImGui.Text($"I therefore would recommend zooming out as much as possible");
                ImGui.Text($"but this is entirely up to preference.");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("The icons are all placed wrong, what gives?"))
            {
                ImGui.TextWrapped("The current math only supports");
                ImGui.Text("a minimap which is locked  (meaning north is always upwards).");
                ImGui.Text("The minimap can be locked with a click on the cog");
                ImGui.Text($"on the minimap HUD element.");
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
            if (ImGui.TreeNode("What means using the map, and not the minimap, as source?"))
            {
                ImGui.TextWrapped($"The map (the one you can open via clicking on the minimap)");
                ImGui.Text($"has more data points of the current area and therefore, in theory,");
                ImGui.Text($"allows the compass to show more icons.");
                ImGui.Text($"When checking this setting, the compass will use the data from the map");
                ImGui.Text($"instead of the minimap to show the icons.");
                ImGui.Text($"This comes with its own trade-offs, however.");
                ImGui.TreePop();
            }
            ImGui.EndTabItem();
            if (ImGui.TreeNode("What trade-offs are there for using the map as source?"))
            {
                ImGui.TextWrapped($"The map needs to be open for the compass to be able");
                ImGui.Text($"to use it as the source. The map can, however, be made 100%% transparent");
                ImGui.Text($"via the settings and/or made very small. It also does not");
                ImGui.Text($"need to be in focus, only open.");
                ImGui.Text($"Currently, there is a hardcoded value for the maximum distance a icon");
                ImGui.Text($"will still appear on the compass. The slider left on the map can be used");
                ImGui.Text($"to adjust this distance somewhat. Quest in progress should always appear,");
                ImGui.Text($"regardless of the distance. As there currently is no way to distinguish between");
                ImGui.Text($"FATE circles and quest circles, all FATEs are treated as quest in progress.");
                ImGui.Text($"Furthermore, gathering points are only available when using the minimap as source.");
                ImGui.Text($"Last but not least, querying the map instead of the minimap is a more taxing");
                ImGui.Text($"operations and needs more resources.");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("Why does the compass keeps closing when I open e.g. the map?"))
            {
                ImGui.TextWrapped($"Open the tree 'Hide Compass when ... window is open.'");
                ImGui.Text($"by clicking on it in the other tab and deselect the window.");
                ImGui.Text($"If one wants to use the map as source,");
                ImGui.Text($"the map should be unchecked, else");
                ImGui.Text($"the compass will be hiding itself when the map is open.");
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
                if (!ImGui.TreeNodeEx(label)) return changed;
                changed |= drawConfig();
                ImGui.TreePop();
            }
            else
            {
                DummyTreeNode(label);
            }
            return changed;
        }

        private static bool DrawOnAddonHideTab(Configuration config, float scale)
        {
            var changed = false;
            if (!config.ImGuiCompassEnable) return changed;
            if (!ImGui.BeginTabItem("Hiding Options")) return changed;
            
            //ImGui.SetWindowFontScale();
            ImGui.Text("Hide Compass when ... window is open.");
            for (var i = 1; i <= config.ShouldHideOnUiObject.Length; i++)
            {
                if (i % 2 == 0) ImGui.SameLine(225);
                changed |= ImGui.Checkbox(
                    config.ShouldHideOnUiObject[i-1].userFacingIdentifier
                    , ref config.ShouldHideOnUiObject[i-1].disable);
                if (changed)
                {
                    config.ShouldHideOnUiObjectSerializer[i-1] = config.ShouldHideOnUiObject[i-1].disable;
                }
            }
            ImGui.EndTabItem();
            return changed;
        }

        private static bool DrawGeneralConfigurationTab(Configuration config, float scale)
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
                if (ImGui.Button("Center horizontally##ImGui", new Vector2(200f, 25f)))
                {
                    changed = true;
                    var screenSizeCenterX = (ImGuiHelpers.MainViewport.Size * 0.5f).X;
                    config.ImGuiCompassPosition = new Vector2(screenSizeCenterX - config.ImGuiCompassWidth * 0.5f,
                        config.ImGuiCompassPosition.Y);
                }
                changed |= ImGui.DragFloat2("Position##ImGui", ref config.ImGuiCompassPosition, 1f, 0f, float.MaxValue, "%.f", ImGuiSliderFlags.AlwaysClamp);
                changed |= ImGui.DragFloat("Width##ImGui", ref config.ImGuiCompassWidth, 1f, 150f, float.MaxValue, "%.f", ImGuiSliderFlags.AlwaysClamp);
               
                changed |= ImGui.SliderFloat("Scale##ImGui", ref config.ImGuiCompassScale, 0.01f, 3f, "%.2f");
                changed |= ImGui.DragInt("Cardinals Offset##ImGui", ref config.ImGuiCompassCardinalsOffset);
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

                    var backgroundStyle = (int) config.ImGuiCompassBackground;
                    ImGui.Text($"Background");
                    changed |= ImGui.RadioButton("Border##ImGui", ref backgroundStyle, 0);
                    ImGui.SameLine();
                    changed |= ImGui.RadioButton("Filled##ImGui", ref backgroundStyle, 1);
                    ImGui.SameLine();
                    changed |= ImGui.RadioButton("Border Filled##ImGui", ref backgroundStyle, 2);
                    ImGui.SameLine();
                    changed |= ImGui.RadioButton("Line##ImGui", ref backgroundStyle, 3);
                    if (changed) config.ImGuiCompassBackground = (ImGuiCompassBackgroundStyle) backgroundStyle;
                    
                    if(config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Filled or ImGuiCompassBackgroundStyle.FilledAndBorder)
                        changed |= ImGui.ColorEdit4("Fill Colour##ImGui", ref config.ImGuiBackgroundColour);
                    if(config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Border or ImGuiCompassBackgroundStyle.FilledAndBorder)
                    {
                        changed |= ImGui.ColorEdit4("Border Colour##ImGui",
                            ref config.ImGuiBackgroundBorderColour);
                        changed |= ImGui.SliderFloat("Border Thickness##ImGui",
                            ref config.ImGuiBackgroundBorderThickness, 1, 10, "%.f", ImGuiSliderFlags.AlwaysClamp);
                    }

                    if (config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Line)
                    {
                        changed |= ImGui.ColorEdit4("Line Colour##ImGui",
                            ref config.ImGuiBackgroundLineColour);
                        changed |= ImGui.SliderFloat("Line Thickness##ImGui",
                            ref config.ImGuiBackgroundLineThickness, 1, 20, "%.f", ImGuiSliderFlags.AlwaysClamp);
                        changed |= ImGui.DragInt("Line Offset##ImGui",
                            ref config.ImGuiCompassBackgroundLineOffset);
                    }

                    if(config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Border or ImGuiCompassBackgroundStyle.FilledAndBorder or ImGuiCompassBackgroundStyle.Filled)
                        changed |= ImGui.SliderFloat("Rounding##ImGui", ref config.ImGuiCompassBackgroundRounding, 0f, 15f, "%.f", ImGuiSliderFlags.AlwaysClamp);
                    return changed;
                });
                changed |= ImGui.Checkbox("Hide Compass when in Combat##ImGui", ref config.HideInCombat);
                changed |= ImGui.Checkbox("Use Map instead of minimap as source##ImGui", ref config.UseAreaMapAsSource);
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextDisabled(FontAwesomeIcon.QuestionCircle.ToIconString());
                ImGui.PopFont();
                if (ImGui.IsItemHovered())
                {
                    
                    ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(ImGui.GetFontSize() * 25.0f);
                    ImGui.TextUnformatted("Please read the FAQ for what that means and caveats.");
                    ImGui.PopTextWrapPos();
                    ImGui.EndTooltip();
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