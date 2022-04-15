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
            ImGui.SetNextWindowSize(new Vector2(400 * scale, 420 * scale), ImGuiCond.FirstUseEver);
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
            ("FATE Markers", new uint[] {060501, 60502, 60503, 60504, 60505, 60506, 60507, 60508, 60458}), // TODO CHECK LAST
            ("Dungeon Symbols", new uint[] {060414}),
            ("Porter", new uint[] {060311}),
            ("Area Transition Markers", new uint[] {060457}),
            ("Stairs & Doors", new uint[] {060446, 60447, 60467, 60971}),
            ("Big Aetheryte", new uint[] {060453}),
            ("Small Aetheryte", new uint[] {060430}),
            ("Map Pins", new uint[] {060442}),
            ("Materia Melder", new uint[] {060910}),
            ("Skywatcher", new uint[] {60581}),
            ("Shops", new uint[] {060412, 60935, 60987}),
            ("Mender", new uint[] {060434}),
            ("Retainer Bell", new uint[] {060560,60425}),
            ("Retainer Advocate", new uint[] {60426}),
            ("Market Board", new uint[] {060570}),
            ("DoW/DoM Guild Symbols",  new uint[] {060319, 060320, 060322, 60330, 60331, 60342, 60344, 60347, 60362, 60363, 60364}),
            ("DoH/DoL Guild Symbols",  new uint[] {060318, 060321, 060326, 60333, 60334, 60335, 60337, 60345, 60346, 60348, 60351}),
            ("Chocobo Companion",  new uint[] {060961}),
            ("Party Members",  new uint[] {060421}),
            ("Enemies",  new uint[] {060422}),
            ("Boss Enemy",  new uint[] {060401}),
            ("Alliance Members",  new uint[] {060358}),
            ("Letter Waymarks",  new uint[] {060474,060475, 060476, 060936}),
            ("Number Waymarks",  new uint[] {060931, 060932, 060933, 063904}),
            ("Red Flag",  new uint[] {060561}),
            ("Delivery Moogle", new uint []{060551}),
            ("Settlements", new uint []{060448}),
            ("Ferry Docks (Anchor)", new uint []{060339}), 
            ("Ferry Docks (Ferry)", new uint []{060456, 63905, 63906}), 
            ("Apartments", new uint []{060789,060790}),
            ("Housing (Locked)", new uint []{060769, 060770, 060771, 060751, 060752, 060753}),
            ("Housing (Open)", new uint []{060772, 060773, 060774, 060754, 060755, 060756}),
            ("Apartments (Own)", new uint []{060791,060792}),
            ("Housing (Own)", new uint []{060776,060777,060778,060779,060780,060781}),
            ("Housing (Shared)", new uint []{060783,060784,060785,060786,060787,060788}),
        };
        
        private static bool DrawFilterTab(Configuration config, float scale)
        {
            var changed = false;
            if (!ImGui.BeginTabItem("Filtering")) return changed;
            ImGui.Text("Filter ... on compass.");
            for (var i = 1; i <= FilterIconIds.Length; i++)
            {
                if (i % 2 == 0) ImGui.SameLine(225 * scale);
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
            var modalSize = new Vector2(400 * scale, 175 * scale);
            var modalPosition = new Vector2(contentSize.X / 2 - modalSize.X / 2, contentSize.Y / 2 - modalSize.Y / 2);
            ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
            if (!ImGui.BeginPopupModal("Compass Note")) return;
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
            ImGui.TextWrapped("Thank you for installing the Compass plugin.\n"
            + "Please have a look at the configuration, especially at the FAQ section, "
            + "for a quick explanation of the current caveats and settings needed "
            + "for the compass to work.\n"
            + "May Hydaelyn protect you in your adventures across Eorzea and beyond!");
            ImGui.PopTextWrapPos();
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
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
            
            if (ImGui.TreeNode("How does this work?"))
            {
                ImGui.TextWrapped($"The current implementation reads the minimap data and "
                                + $"displays it on the monodimensional compass.\n"
                                + $"This also works when the minimap HUD element is hidden.");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("What are the current limitations?"))
            {
                ImGui.TextWrapped($"Because of the current implementation, "
                                + $"only data the minimap knows about can be displayed. "
                                + $"This usually means icons in the immediate vicinity "
                                + $"of the player plus objective markers for the current tracked quests.\n"
                                + $"Different zoom levels of the minimap influence not the overall "
                                + $"distance but how soon the 'arrow' icon for tracked quests "
                                + $"turns into a more proper goal icon (e.g. area circle).\n"
                                + $"I therefore recommend to zoom out as much as possible "
                                + $"but this is entirely up to preference.");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("The icons are all placed wrong, what gives?"))
            {
                ImGui.TextWrapped("The current math only supports "
                                + "a minimap which is locked  (meaning north is always upwards). "
                                + "The minimap can be locked with a click on the cog "
                                + $"on the minimap HUD element. "
                                + "(In the case of Material UI, its a lock, not a cog.)");
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("How can I resize the compass?"))
            {
                ImGui.TextWrapped($"The icons on the compass can be resized in this settings menu "
                                + $"(other tab), the overall size of the compass (e.g. the height) is "
                                + $"influenced by the global font scale set in Dalamud's settings.\n"
                                + $"After modifying the font scale in Dalamud, a compass' settings needs to be changed " 
                                + $"(e.g. disable/enable the compass) to recalculate the size.");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Why does filtering FATE markers does not filter out the FATE area circles?"))
            {
                ImGui.TextWrapped($"Currently, there is no way to distinguish between "
                                  + $"FATE circles and quest circles. Each circle is therefore treated as belonging "
                                  + $"to a quest-in-progress and therefore always shown.\n");
                ImGui.TreePop();
            }
            
            if (ImGui.TreeNode("What means using the map, and not the minimap, as source?"))
            {
                ImGui.TextWrapped($"The map (the one you can open via clicking on the minimap) "
                                + $"has more data points of the current area and therefore, in theory, "
                                + $"allows the compass to show more icons. "
                                + $"When checking this setting, the compass will use the data from the map"
                                + $"instead of the minimap to show the icons.\n"
                                + $"This comes with its own trade-offs, however.");
                ImGui.TreePop();
            }
            
            if (ImGui.TreeNode("What trade-offs are there for using the map as source?"))
            {
                ImGui.TextWrapped($"The map needs to be open for the compass to be able "
                                  + $"to use it as the source. The map can, however, be made 100%% transparent "
                                  + $"via the settings and/or made very small. It also does not "
                                  + $"need to be in focus, only open.\n"
                                  + $"Furthermore, gathering points are only available when using the minimap as source. "
                                  + $"Last but not least, querying the map instead of the minimap is a more taxing "
                                  + $"operation and needs more resources.");
                ImGui.TreePop();
            }
            
            if (ImGui.TreeNode("The compass is wrong/missing after a DeepDungeon/Chocobo race/Lord of Verminion match etc.?"))
            {
                ImGui.TextWrapped($"Please type '/compass on' or toggle the compass in the settings tab " +
                                  $"to reset the internal cache. Visiting a DeepDungeon/Chocobo race etc. unfortunately kills the mini map " +
                                  $"completely and as such the cache needs to be reset for the compass to work again.");
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
            if (!ImGui.BeginTabItem("Hiding Options")) return changed;
            
            //ImGui.SetWindowFontScale();
            ImGui.Text("Hide Compass when ... window is open.");
            for (var i = 1; i <= config.ShouldHideOnUiObject.Length; i++)
            {
                if (i % 2 == 0) ImGui.SameLine(225 * scale);
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
                var mask = (int)((1 - config.ImGuiCompassReverseMaskPercentage) * 100);
                changed |= ImGui.SliderInt("Mask###ImGuiCompassMask", ref mask, 0, 90,
                    "%d %%");
                config.ImGuiCompassReverseMaskPercentage = 1 - (mask / 100f);
                changed |= ImGui.SliderFloat("Scale##ImGui", ref config.ImGuiCompassScale, 0.02f, 3f, "%.2f");
                changed |= ImGui.SliderFloat("Minimum Scale Factor##ImGuiCompass_AreaMap", ref config.ImGuiCompassMinimumIconScaleFactor, 0.00f, 1f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
                changed |= ImGui.DragInt("Cardinals Offset##ImGui", ref config.ImGuiCompassCardinalsOffset);
                var visibility = (int) config.Visibility;
                ImGui.Text($"Visibility");
                changed |= ImGui.RadioButton("Always##ImGuiCompass", ref visibility, 0);
                ImGui.SameLine();
                changed |= ImGui.RadioButton("Not in Combat##ImGuiCompass", ref visibility, 1);
                ImGui.SameLine();
                changed |= ImGui.RadioButton("Only in Combat##ImGuiCompass", ref visibility, 2);
                if (changed) config.Visibility = (CompassVisibility) visibility;
                changed |= ImGui.Checkbox("Show only Cardinals##ImGui", ref config.ShowOnlyCardinals);
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
                changed |= DrawTreeCheckbox("Show Weather Icon", ref config.ShowWeatherIcon, () =>
                {
                    var changed = false;
                    changed |= ImGui.Checkbox("Show Border##ImGuiCompass_WeatherIcon", ref config.ShowWeatherIconBorder);
                    changed |= ImGui.DragFloat2("Offset##ImGuiCompass_WeatherIcon", ref config.ImGuiCompassWeatherIconOffset, 1f);
                    changed |= ImGui.SliderFloat("Scale##ImGuiCompass_WeatherIcon", ref config.ImGuiCompassWeatherIconScale, 0.01f, 3f, "%.2f");
                    return changed;
                });
                changed |= DrawTreeCheckbox("Show approx. Distance To Target", ref config.ShowDistanceToTarget, () =>
                {
                    var changed = false;
                    changed |= ImGui.DragFloat2("Offset##ImGuiCompass_DistanceToTarget", ref config.ImGuiCompassDistanceToTargetOffset, 1f);
                    changed |= ImGui.SliderFloat("Scale##ImGuiCompass_DistanceToTarget", ref config.ImGuiCompassDistanceToTargetScale, 0.01f, 3f, "%.2f");
                    ImGui.InputText("Prefix##ImGuiCompass_DistanceToTarget",
                        ref config.DistanceToTargetPrefix, 99);
                    changed |= ImGui.InputText("Suffix##ImGuiCompass_DistanceToTarget",
                        ref config.DistanceToTargetSuffix, 99);
                    changed |= ImGui.ColorEdit4("Colour##ImGuiCompass_DistanceToTarget", ref config.ImGuiCompassDistanceToTargetColour);
                    changed |= ImGui.Checkbox("Prioritise MouseOver Target",
                        ref config.ImGuiCompassDistanceToTargetMouseOverPrio);
                    return changed;
                });
                changed |= DrawTreeCheckbox("Use Map instead of Minimap as source##ImGui",
                    ref config.UseAreaMapAsSource,
                    () =>
                    {
                        var changed = false;
                        changed |= ImGui.DragFloat("Max Distance##ImGui_AreaMap", ref config.AreaMapMaxDistance, 10f, 80f, 2000f, "%.f", ImGuiSliderFlags.AlwaysClamp);
                        changed |= ImGui.SliderFloat("Minimum Scale Factor##ImGuiCompass_AreaMap", ref config.ImGuiCompassMinimumIconScaleFactorAreaMap, 0.00f, 1f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
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