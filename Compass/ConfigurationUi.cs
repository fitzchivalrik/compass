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
            
            if (config.FreshInstall) DrawFreshInstallNotice(config, scale);

            if(ImGui.BeginTabBar("ConfigTabBar", ImGuiTabBarFlags.NoTooltip))
            {
                changed |= DrawGeneralConfigurationTab(config, scale);
                changed |= DrawFilterTab(config, scale);
                changed |= DrawOnAddonHideTab(config, scale);
                DrawHelpTab();
                ImGui.EndTabBar();
            }
            
            ImGui.End();
            return (shouldBuildConfigUi, changed);
        }


        private static readonly (string description, uint[] ids)[] FilterIconIds =
        {
            ("MSQ Quests", new uint[] {71001, 71002}),
            ("Locked MSQ Quests", new uint[] {071011, 71012}),
            ("Blue Quests", new uint[] {071141, 71142}),
            ("Locked Blue Quests", new uint[] {071151, 71152}),
            ("Side Quests", new uint[] {071021, 71022, 71111}),
            ("Locked Side Quests", new uint[] {071031, 71032}),
            ("Lore (Book) Quests", new uint[] {071061, 71062}),
            ("Locked Lore (Book) Quests", new uint[] {071071, 71072}),
            ("Levequests", new uint[] {071081, 71082, 071041}),
            ("Triple Triad Regular", new uint[] {071101}),
            ("Triple Triad New Cards", new uint[] {071102}),
            ("Fate Markers", new uint[] {060501, 60502, 60503, 60504, 60505, 60506, 60507, 60508}),
            ("Dungeon Symbols", new uint[] {060414}),
            ("Porter", new uint[] {060311}),
            ("Area Transition Markers", new uint[] {060457}),
            ("Stairs & Doors", new uint[] {060446, 60447, 60467, 60971}),
            ("Big Aetheryte", new uint[] {060453}),
            ("Small Aetheryte", new uint[] {060430}),
            ("Map Pins", new uint[] {060442}),
            ("Materia Melder", new uint[] {060910}),
            ("Skywatcher", new uint[] {60581}),
            ("Shops", new uint[] {060412, 60935}),
            ("Mender", new uint[] {060434}),
            ("Retainer Bell", new uint[] {060560,60425}),
            ("Market Board", new uint[] {060570}),
            ("DoW/DoM Guild Symbols",  new uint[] {060319, 060320, 060322, 60330, 60331, 60342, 60344, 60347, 60362, 60363, 60364}),
            ("DoH/DoL Guild Symbols",  new uint[] {060318, 060321, 060326, 60333, 60334, 60335, 60337, 60345, 60346, 60348, 60351}),
        };
        
        private static bool DrawFilterTab(Configuration config, float scale)
        {
            var changed = false;
            if (!ImGui.BeginTabItem("Filtering")) return changed;
            ImGui.Text("Filter ... on compass.");
            for (var i = 1; i <= FilterIconIds.Length; i++)
            {
                if (i % 2 == 0) ImGui.SameLine(225);
                changed |= DrawFilterCheckBox(config, FilterIconIds[i-1].description, FilterIconIds[i-1].ids);
            }
            ImGui.EndTabItem();
            return changed;
        }

        private static bool DrawFilterCheckBox(Configuration config, string description, params uint[] iconId)
        {
            var iconContained = config.FilteredIconIds.Contains(iconId[0]);
            if (!ImGui.Checkbox(description, ref iconContained)) return false;
            foreach (var id in iconId) config.FilteredIconIds.Remove(id);
            if (!iconContained) return true;
            foreach (var id in iconId) config.FilteredIconIds.Add(id);
            return true;
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
            // TODO TextUnformatted with ImGui.PushTextWrapPos();
            if (!ImGui.BeginTabItem("FAQ")) return;
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            
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
            
            ImGui.PopTextWrapPos();
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
               
                changed |= ImGui.SliderFloat("Scale##ImGui", ref config.ImGuiCompassScale, 0.02f, 3f, "%.2f");
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
                changed |= DrawTreeCheckbox("Enable Background", ref config.ImGuiCompassEnableBackground, DrawBackgroundConfig(config));
                changed |= ImGui.Checkbox("Hide Compass when in Combat##ImGui", ref config.HideInCombat);
                changed |= DrawTreeCheckbox("Use Map instead of minimap as source##ImGui",
                    ref config.UseAreaMapAsSource,
                    () =>
                    {
                        var changed = false;
                        changed |= ImGui.DragFloat("Max Distance##ImGui", ref config.AreaMapMaxDistance, 10f, 80f, 2000f, "%.f", ImGuiSliderFlags.AlwaysClamp);
                        return changed;
                    });
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

        private static Func<bool> DrawBackgroundConfig(Configuration config)
        {
            return () =>
            {
                var changed = false;

                var backgroundStyle = (int) config.ImGuiCompassBackground;
                ImGui.Text($"Background");
                changed |= ImGui.RadioButton("Filled##ImGui", ref backgroundStyle, 0);
                ImGui.SameLine();
                changed |= ImGui.RadioButton("Border##ImGui", ref backgroundStyle, 1);
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
            };
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