using System;
using System.Numerics;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using SimpleTweaksPlugin;

namespace Compass
{
    public partial class Compass : IDalamudPlugin
    {
        public const string PluginName = "Compass";
        private const string Command = "/compass";
        public string Name => PluginName;

        private readonly Configuration _config;
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly ClientState _clientState;
        private readonly CommandManager _commands;
        private readonly TargetManager _targetManager;
        private readonly Condition _condition;
        private readonly GameGui _gameGui;

        private string[] _uiIdentifiers = null!; //Constructor calls method which initializes
        private int _currentUiObjectIndex;
        private bool _shouldHideCompass;
        private bool _shouldHideCompassIteration;
        private bool _isDisposed;
        private bool _buildingConfigUi;


        public Compass(DalamudPluginInterface pi
            , SigScanner sigScanner
            , ClientState clientState
            , CommandManager commands
            , Condition condition
            , TargetManager targetManager
            , [RequiredVersion("1.0")] GameGui gameGui)
        {
            #region Signatures

            #endregion

            var config = pi.GetPluginConfig() as Configuration ?? new Configuration();
            _pluginInterface = pi;
            _config = config;
            _clientState = clientState;
            _commands = commands;
            _condition = condition;
            _gameGui = gameGui;
            _targetManager = targetManager;
            unsafe
            {
                _targetSystem = (TargetSystem*)_targetManager.Address;
            }
            
            #region Configuration Setup

            _config.ShouldHideOnUiObject = new[]
            {
                (new[] { "_BattleTalk" }, false, "Dialogue Box During Battle"),
                (new[] { "Talk" }, false, "Dialogue Box"), (new[] { "AreaMap" }, false, "Map"),
                (new[] { "Character" }, false, "Character"),
                (new[] { "ConfigCharacter" }, false, "Character Configuration"),
                (new[] { "ConfigSystem" }, false, "System Configuration"),
                (new[] { "Inventory", "InventoryLarge", "InventoryExpansion" }, false, "Inventory"),
                (new[] { "InventoryRetainer", "InventoryRetainerLarge" }, false, "Retainer Inventory"),
                (new[] { "InventoryBuddy" }, false, "Saddle Bag"), (new[] { "ArmouryBoard" }, false, "Armoury"),
                (new[] { "Shop", "InclusionShop", "ShopExchangeCurrency" }, false, "Shops"),
                (new[] { "Teleport" }, false, "Teleport"), (new[] { "ContentsInfo" }, false, "Timers"),
                (new[] { "ContentsFinder" }, false, "Duty Finder"),
                (new[] { "LookingForGroup" }, false, "Party Finder"),
                (new[] { "AOZNotebook" }, false, "Bluemage Notebook"),
                (new[] { "MountNoteBook" }, false, "Mount Guide"), (new[] { "MinionNoteBook" }, false, "Minion Guide"),
                (new[] { "Achievement" }, false, "Achievements"), (new[] { "GoldSaucerInfo" }, false, "Action Help"),
                (new[] { "PvpProfile" }, false, "PVP Profile"), (new[] { "LinkShell" }, false, "Linkshell"),
                (new[] { "CrossWorldLinkshell" }, false, "Crossworld Linkshell"),
                (new[] { "ActionDetail" }, false, "Action Help (Tooltip)"),
                (new[] { "ItemDetail" }, false, "Item Tooltip"), (new[] { "ActionMenu" }, false, "Action List"),
                (new[] { "QuestRedo", "QuestRedoHud" }, false, "New Game+"), (new[] { "Journal" }, false, "Journal"),
                (new[] { "RecipeNote" }, false, "Crafting Log"),
                (new[] { "AdventureNoteBook" }, false, "Sightseeing Log"),
                (new[] { "GatheringNote" }, false, "Gathering Log"), (new[] { "FishingNote" }, false, "Fishing Log"),
                (new[] { "FishGuide" }, false, "Fishing Guide"), (new[] { "Orchestrion" }, false, "Orchestrion List"),
                (new[] { "ContentsNote" }, false, "Challenge Log"), (new[] { "MonsterNote" }, false, "Hunting Log"),
                (new[] { "PartyMemberList" }, false, "Party Members"), (new[] { "FriendList" }, false, "Friend list"),
                (new[] { "BlackList" }, false, "Black List"), (new[] { "SocialList" }, false, "Player Search"),
                (new[] { "Emote" }, false, "Emote"), (new[] { "FreeCompany" }, false, "Free Company"),
                (new[] { "SupportDesk" }, false, "Support Desk"), (new[] { "ConfigKeybind" }, false, "Keybinds"),
                (new[] { "_HudLayoutScreen" }, false, "HUD Layout"), (new[] { "Macro" }, false, "Macro"),
                (new[] { "GrandCompanySupplyList" }, false, "Grand Company Delivery"),
                (new[] { "GrandCompanyExchange" }, false, "Grand Company Shop"),
                (new[] { "MiragePrismPrismBox" }, false, "Glamour Dresser"), (new[] { "Currency" }, false, "Currency"),
                (new[] { "_MainCross" }, false, "Controller Main Menu"),
                (new[] { "JournalResult" }, false, "Quest Complete"),
                (new[] { "Synthesis" }, false, "Crafting (Synthesis)")
            };

            for (var i = 0; i < _config.ShouldHideOnUiObjectSerializer.Length; i++)
            {
                _config.ShouldHideOnUiObject[i].disable = _config.ShouldHideOnUiObjectSerializer[i];
            }

            if (_config.ShouldHideOnUiObjectSerializer.Length < _config.ShouldHideOnUiObject.Length)
            {
                Array.Resize(ref _config.ShouldHideOnUiObjectSerializer, _config.ShouldHideOnUiObject.Length);
            }

            #endregion


            _clientState.Login += OnLogin;
            _clientState.Logout += OnLogout;

            #region Hooks, Functions and Addresses

            #endregion

            #region Excel Data

            #endregion

            _commands.AddHandler(Command, new CommandInfo((_, args) =>
            {
                switch (args)
                {
                    case "toggle":
                        _config.ImGuiCompassEnable ^= true;
                        UpdateCompassVariables();
                        break;
                    case "on":
                        _config.ImGuiCompassEnable = true;
                        UpdateCompassVariables();
                        break;
                    case "off":
                        _config.ImGuiCompassEnable = false;
                        break;
                    case "togglep":
                        _config.ImGuiCompassEnable ^= true;
                        UpdateCompassVariables();
                        _pluginInterface.SavePluginConfig(_config);
                        break;
                    case "onp":
                        _config.ImGuiCompassEnable = true;
                        UpdateCompassVariables();
                        _pluginInterface.SavePluginConfig(_config);
                        break;
                    case "offp":
                        _config.ImGuiCompassEnable = false;
                        _pluginInterface.SavePluginConfig(_config);
                        break;
                    default:
                        OnOpenConfigUi();
                        break;
                }
            })
            {
                HelpMessage =
                    $"Open {PluginName} configuration menu. Use \"{Command} <toggle|on|off>\" to enable/disable. Add 'p' to the command to also save the state (<togglep|onp|offp>)",
                ShowInHelp = true
            });

            DebugCtor(sigScanner);
            if (_pluginInterface.Reason == PluginLoadReason.Installer)
            {
                // NOTE (Chiv) Centers the compass on first install
                SimpleLog.Information("Fresh install of compass; centering compass, drawing modal.");
                var screenSizeCenterX = (ImGuiHelpers.MainViewport.Size * 0.5f).X;
                config.ImGuiCompassPosition = new Vector2(screenSizeCenterX - config.ImGuiCompassWidth * 0.5f,
                    config.ImGuiCompassPosition.Y);
                _buildingConfigUi = true;
                _config.FreshInstall = true;
                _pluginInterface.UiBuilder.Draw += DrawConfigUi;
            }
            if (_clientState.LocalPlayer is not null)
            {
                OnLogin(null!, null!);
            }
        }

        private void OnLogout(object? sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            _pluginInterface.UiBuilder.Draw -= DrawConfigUi;

            _pluginInterface.UiBuilder.Draw -= DrawImGuiCompass;
        }

        private void OnLogin(object? sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            UpdateCompassVariables();
            _pluginInterface.UiBuilder.Draw += DrawImGuiCompass;
        }

        #region UI

        private void DrawConfigUi()
        {
            var (shouldBuildConfigUi, changedConfig) = ConfigurationUi.DrawConfigUi(_config);
            if (changedConfig)
            {
                _pluginInterface.SavePluginConfig(_config);
                UpdateCompassVariables();
            }

            if (shouldBuildConfigUi) return;
            _pluginInterface.UiBuilder.Draw -= DrawConfigUi;
            _buildingConfigUi = false;
        }

        private void OnOpenConfigUi()
        {
            _buildingConfigUi = !_buildingConfigUi;
            if (_buildingConfigUi)
                _pluginInterface.UiBuilder.Draw += DrawConfigUi;
            else
                _pluginInterface.UiBuilder.Draw -= DrawConfigUi;
        }

        #endregion

        #region Debug Partials

        partial void DebugCtor(SigScanner sigScanner);
        partial void DebugDtor();

        #endregion

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                // TODO (Chiv) Still not quite sure about correct dispose
                // NOTE (Chiv) Explicit, non GC? call - remove managed thingies too.
                OnLogout(null!, null!);
                _clientState.Login -= OnLogin;
                _clientState.Logout -= OnLogout;
                _commands.RemoveHandler(Command);
                DebugDtor();
            }

            _isDisposed = true;
        }

        ~Compass()
        {
            Dispose(false);
        }

        #endregion
    }
}