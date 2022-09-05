using System;
using System.Numerics;
using Compass.Data;
using Compass.Resources;
using Dalamud.Interface;
using ImGuiNET;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace Compass.UI;

internal static class Config {
    internal static (bool shouldBuildConfigUi, bool changedConfig) Draw(Configuration config) {
        var shouldBuildConfigUi = true;
        var changed             = false;
        var scale               = ImGui.GetIO().FontGlobalScale;
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowSize(new Vector2(400 * scale, 420 * scale), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(410 * scale, 200 * scale),
            new Vector2(float.MaxValue, float.MaxValue));
        if (!ImGui.Begin($"{Plugin.PluginName} Configuration", ref shouldBuildConfigUi,
                ImGuiWindowFlags.NoCollapse)) {
            ImGui.End();
            return (shouldBuildConfigUi, changed);
        }

        if (config.FreshInstall) DrawFreshInstallNotice(config, scale);

        if (ImGui.BeginTabBar("ConfigTabBar", ImGuiTabBarFlags.NoTooltip)) {
            changed |= DrawGeneralConfigurationTab(config, scale);
            changed |= DrawFilterTab(config, scale);
            changed |= DrawOnAddonHideTab(config, scale);
            DrawHelpTab();
            ImGui.EndTabBar();
        }

        ImGui.End();
        return (shouldBuildConfigUi, changed);
    }

    private static bool DrawFilterTab(Configuration config, float scale) {
        var changed = false;
        if (!ImGui.BeginTabItem(i18n.tab_header_filter)) return changed;
        ImGui.Text(i18n.filter_tab_text);
        for (var i = 1; i <= Constant.FilterIconIds.Length; i++) {
            if (i % 2 == 0) ImGui.SameLine(225 * scale);
            changed |= DrawFilterCheckBox(config, Constant.FilterIconIds[i - 1].description, Constant.FilterIconIds[i - 1].ids);
        }

        ImGui.EndTabItem();
        return changed;
    }

    private static bool DrawFilterCheckBox(Configuration config, string description, params uint[] iconId) {
        var iconContained = config.FilteredIconIds.Contains(iconId[0]);
        if (!ImGui.Checkbox(description, ref iconContained)) return false;
        foreach (var id in iconId) config.FilteredIconIds.Remove(id);
        if (!iconContained) return true;
        foreach (var id in iconId) config.FilteredIconIds.Add(id);
        return true;
    }

    private static void DrawFreshInstallNotice(Configuration config, float scale) {
        ImGui.OpenPopup("Compass Note");
        var contentSize   = ImGuiHelpers.MainViewport.Size;
        var modalSize     = new Vector2(400 * scale, 175 * scale);
        var modalPosition = new Vector2(contentSize.X / 2 - modalSize.X / 2, contentSize.Y / 2 - modalSize.Y / 2);
        ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
        if (!ImGui.BeginPopupModal("Compass Note")) return;
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
        ImGui.TextWrapped(i18n.fresh_install_note);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        if (ImGui.Button($"{i18n.fresh_install_note_confirm_button}###Compass_FreshInstallPopUp")) {
            config.FreshInstall = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static void DrawHelpTab() {
        if (!ImGui.BeginTabItem(i18n.tab_header_faq)) return;
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);

        if (ImGui.TreeNode(i18n.faq_how_does_this_work_header)) {
            ImGui.TextWrapped(i18n.faq_how_does_this_work_entry);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode(i18n.faq_limitations_header)) {
            ImGui.TextWrapped(i18n.faq_limitations_entry);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode(i18n.faq_wrong_placed_icons_header)) {
            ImGui.TextWrapped(i18n.faq_wrong_placed_icons_entry);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode(i18n.faq_resize_header)) {
            ImGui.TextWrapped(i18n.faq_resize_entry);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode(i18n.faq_fate_no_filter_circles_header)) {
            ImGui.TextWrapped(i18n.faq_fate_no_filter_circles_entry);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode(i18n.faq_use_map_header)) {
            ImGui.TextWrapped(i18n.faq_use_map_entry);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode(i18n.faq_use_map_tradeoffs_header)) {
            ImGui.TextWrapped(i18n.faq_use_map_tradeoffs_entry);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode(i18n.faq_compass_gone_header)) {
            ImGui.TextWrapped(i18n.faq_compass_gone_entry);
            ImGui.TreePop();
        }


        ImGui.PopTextWrapPos();
        ImGui.EndTabItem();
    }

    private static bool DrawOnAddonHideTab(Configuration config, float scale) {
        var changed = false;
        if (!ImGui.BeginTabItem(i18n.tab_header_hiding_options)) return changed;

        ImGui.Text(i18n.hide_compass_text);
        for (var i = 1; i <= config.ShouldHideOnUiObject.Length; i++) {
            if (i % 2 == 0) ImGui.SameLine(225 * scale);
            changed |= ImGui.Checkbox(
                config.ShouldHideOnUiObject[i - 1].userFacingIdentifier
                , ref config.ShouldHideOnUiObject[i - 1].disable);
            if (changed) config.ShouldHideOnUiObjectSerializer[i - 1] = config.ShouldHideOnUiObject[i - 1].disable;
        }

        ImGui.EndTabItem();
        return changed;
    }

    private static bool DrawGeneralConfigurationTab(Configuration config, float scale) {
        if (!ImGui.BeginTabItem(i18n.tab_header_settings)) return false;
        var changed = false;
        changed |= ImGui.Checkbox("Enable Compass", ref config.ImGuiCompassEnable);
        if (config.ImGuiCompassEnable) {
            ImGui.Indent();
            ImGui.PushItemWidth(200f * scale);

            if (ImGui.Button("Center horizontally##ImGui", new Vector2(200f, 25f))) {
                changed = true;
                var screenSizeCenterX = (ImGuiHelpers.MainViewport.Size * 0.5f).X;
                config.ImGuiCompassPosition = config.ImGuiCompassPosition with { X = screenSizeCenterX - config.ImGuiCompassWidth * 0.5f };
            }

            changed |= ImGui.DragFloat2("Position##ImGui", ref config.ImGuiCompassPosition, 1f, float.MinValue, float.MaxValue, "%.f");
            changed |= ImGui.DragFloat("Width##ImGui", ref config.ImGuiCompassWidth, 1f, 150f, float.MaxValue, "%.f", ImGuiSliderFlags.AlwaysClamp);
            var mask = (int)((1 - config.ImGuiCompassReverseMaskPercentage) * 100);
            changed |= ImGui.SliderInt("Mask###ImGuiCompassMask", ref mask, 0, 90,
                "%d %%");

            config.ImGuiCompassReverseMaskPercentage =  1 - mask / 100f;
            changed                                  |= ImGui.SliderFloat("Scale##ImGui", ref config.ImGuiCompassScale, 0.02f, 3f, "%.2f");
            changed |= ImGui.SliderFloat("Minimum Scale Factor##ImGuiCompass_AreaMap", ref config.ImGuiCompassMinimumIconScaleFactor, 0.00f, 1f, "%.2f",
                ImGuiSliderFlags.AlwaysClamp);

            changed |= ImGui.DragInt("Cardinals Offset##ImGui", ref config.ImGuiCompassCardinalsOffset);

            var visibility = (int)config.Visibility;
            ImGui.Text("Visibility");
            changed |= ImGui.RadioButton("Always##ImGuiCompass", ref visibility, 0);
            ImGui.SameLine();
            changed |= ImGui.RadioButton("Not in Combat##ImGuiCompass", ref visibility, 1);
            ImGui.SameLine();
            changed |= ImGui.RadioButton("Only in Combat##ImGuiCompass", ref visibility, 2);
            if (changed) config.Visibility = (CompassVisibility)visibility;

            changed |= ImGui.Checkbox("Show Cardinals##ImGui", ref config.ShowCardinals);
            changed |= ImGui.Checkbox("Show Intercardinals##ImGui", ref config.ShowInterCardinals);
            changed |= ImGui.Checkbox("Hide all icons##ImGui", ref config.ShowOnlyCardinals);
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 25.0f);
                ImGui.TextUnformatted("Except Cardinals & Intercardinals, if enabled.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }

            changed |= ImGuiHelper.DrawTreeCheckbox("Enable Centre Marker", ref config.ImGuiCompassEnableCenterMarker, () => {
                var changed = false;
                ImGui.SetNextItemWidth(35 * scale);
                changed |= ImGui.DragInt("Centre Marker Offset##ImGui",
                    ref config.ImGuiCompassCentreMarkerOffset);
                changed |= ImGui.Checkbox("Flip Centre Marker##ImGui", ref config.ImGuiCompassFlipCentreMarker);
                return changed;
            });

            changed |= ImGuiHelper.DrawTreeCheckbox("Enable Background", ref config.ImGuiCompassEnableBackground, DrawBackgroundConfig(config));

            changed |= ImGuiHelper.DrawTreeCheckbox("Show Weather Icon", ref config.ShowWeatherIcon, () => {
                var changed = false;
                changed |= ImGui.Checkbox("Show Border##ImGuiCompass_WeatherIcon", ref config.ShowWeatherIconBorder);
                changed |= ImGui.DragFloat2("Offset##ImGuiCompass_WeatherIcon", ref config.ImGuiCompassWeatherIconOffset, 1f);
                changed |= ImGui.SliderFloat("Scale##ImGuiCompass_WeatherIcon", ref config.ImGuiCompassWeatherIconScale, 0.01f, 3f, "%.2f");
                return changed;
            });

            changed |= ImGuiHelper.DrawTreeCheckbox("Show approx. Distance To Target", ref config.ShowDistanceToTarget, () => {
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

            changed |= ImGuiHelper.DrawTreeCheckbox("Use Map instead of Minimap as source##ImGui",
                ref config.UseAreaMapAsSource,
                () => {
                    var changed = false;
                    changed |= ImGui.DragFloat("Max Distance##ImGui_AreaMap", ref config.AreaMapMaxDistance, 10f, 80f, 2000f, "%.f",
                        ImGuiSliderFlags.AlwaysClamp);
                    changed |= ImGui.SliderFloat("Minimum Scale Factor##ImGuiCompass_AreaMap", ref config.ImGuiCompassMinimumIconScaleFactorAreaMap, 0.00f, 1f,
                        "%.2f", ImGuiSliderFlags.AlwaysClamp);
                    return changed;
                });
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextDisabled(FontAwesomeIcon.QuestionCircle.ToIconString());
            ImGui.PopFont();
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 25.0f);
                ImGui.TextUnformatted(i18n.please_read_faq);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }

            ImGui.PopItemWidth();
            ImGui.Unindent();
        }

        ImGui.EndTabItem();
        return changed;
    }

    private static Func<bool> DrawBackgroundConfig(Configuration config) {
        return () => {
            var changed = false;

            var backgroundStyle = (int)config.ImGuiCompassBackground;
            ImGui.Text("Background");
            changed |= ImGui.RadioButton("Filled##ImGui", ref backgroundStyle, 0);
            ImGui.SameLine();
            changed |= ImGui.RadioButton("Border##ImGui", ref backgroundStyle, 1);
            ImGui.SameLine();
            changed |= ImGui.RadioButton("Border Filled##ImGui", ref backgroundStyle, 2);
            ImGui.SameLine();
            changed |= ImGui.RadioButton("Line##ImGui", ref backgroundStyle, 3);
            if (changed) config.ImGuiCompassBackground = (ImGuiCompassBackgroundStyle)backgroundStyle;

            if (config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Filled or ImGuiCompassBackgroundStyle.FilledAndBorder)
                changed |= ImGui.ColorEdit4("Fill Colour##ImGui", ref config.ImGuiBackgroundColour);
            if (config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Border or ImGuiCompassBackgroundStyle.FilledAndBorder) {
                changed |= ImGui.ColorEdit4("Border Colour##ImGui",
                    ref config.ImGuiBackgroundBorderColour);
                changed |= ImGui.SliderFloat("Border Thickness##ImGui",
                    ref config.ImGuiBackgroundBorderThickness, 1, 10, "%.f", ImGuiSliderFlags.AlwaysClamp);
            }

            if (config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Line) {
                changed |= ImGui.ColorEdit4("Line Colour##ImGui",
                    ref config.ImGuiBackgroundLineColour);
                changed |= ImGui.SliderFloat("Line Thickness##ImGui",
                    ref config.ImGuiBackgroundLineThickness, 1, 20, "%.f", ImGuiSliderFlags.AlwaysClamp);
                changed |= ImGui.DragInt("Line Offset##ImGui",
                    ref config.ImGuiCompassBackgroundLineOffset);
            }

            if (config.ImGuiCompassBackground is ImGuiCompassBackgroundStyle.Border or ImGuiCompassBackgroundStyle.FilledAndBorder
                or ImGuiCompassBackgroundStyle.Filled)
                changed |= ImGui.SliderFloat("Rounding##ImGui", ref config.ImGuiCompassBackgroundRounding, 0f, 15f, "%.f", ImGuiSliderFlags.AlwaysClamp);
            return changed;
        };
    }
}