using System;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Compass
{
    public unsafe partial class Compass : IDisposable
    {
        public const string PluginName = "Compass";
        private const string Command = "/compass";
        
        private readonly Configuration _config;
        private readonly DalamudPluginInterface _pluginInterface;
        
        private string[] _uiIdentifiers = null!; //Constructor calls method which initializes
        private int _currentUiObjectIndex;
        private bool _shouldHideCompass;
        private bool _shouldHideCompassIteration;
        private bool _isDisposed;
        private bool _buildingConfigUi;
        

        public Compass(DalamudPluginInterface pi, Configuration config)
        {
            #region Signatures

            #endregion


            _pluginInterface = pi;
            _config = config;

            #region Configuration Setup

            _config.ShouldHideOnUiObject = new[]
            {
                  (new [] {"_BattleTalk"}, false, "Dialogue Box During Battle")
                , (new [] {"Talk"}, false, "Dialogue Box")
                , (new [] {"AreaMap"}, false, "Map")
                , (new [] {"Character"}, false, "Character")
                , (new [] {"ConfigCharacter"}, false, "Character Configuration")
                , (new [] {"ConfigSystem"}, false, "System Configuration")
                , (new [] {"Inventory", "InventoryLarge", "InventoryExpansion"}, false, "Inventory")
                , (new [] {"InventoryRetainer", "InventoryRetainerLarge"}, false, "Retainer Inventory")
                , (new [] {"InventoryBuddy"}, false, "Saddle Bag")
                , (new [] {"ArmouryBoard"}, false, "Armoury")
                , (new [] {"Shop", "InclusionShop", "ShopExchangeCurrency"}, false, "Shops")
                , (new [] {"Teleport"}, false, "Teleport")
                , (new [] {"ContentsInfo"}, false, "Timers")
                , (new [] {"ContentsFinder"}, false, "Duty Finder")
                , (new [] {"LookingForGroup"}, false, "Party Finder")
                , (new [] {"AOZNotebook"}, false, "Bluemage Notebook")
                , (new [] {"MountNoteBook"}, false, "Mount Guide")
                , (new [] {"MinionNoteBook"}, false, "Minion Guide")
                , (new [] {"Achievement"}, false, "Achievements")
                , (new [] {"GoldSaucerInfo"}, false, "Action Help")
                , (new [] {"PvpProfile"}, false, "PVP Profile")
                , (new [] {"LinkShell"}, false, "Linkshell")
                , (new [] {"CrossWorldLinkshell"}, false, "Crossworld Linkshell")
                , (new [] {"ActionDetail"}, false, "Action Help (Tooltip)")
                , (new [] {"ItemDetail"}, false, "Item Tooltip")
                , (new [] {"ActionMenu"}, false, "Action List")
                , (new [] {"QuestRedo", "QuestRedoHud"}, false, "New Game+")
                , (new [] {"Journal"}, false, "Journal")
                , (new [] {"RecipeNote"}, false, "Crafting Log")
                , (new [] {"AdventureNoteBook"}, false, "Sightseeing Log")
                , (new [] {"GatheringNote"}, false, "Gathering Log")
                , (new [] {"FishingNote"}, false, "Fishing Log")
                , (new [] {"FishGuide"}, false, "Fishing Guide")
                , (new [] {"Orchestrion"}, false, "Orchestrion List")
                , (new [] {"ContentsNote"}, false, "Challenge Log")
                , (new [] {"MonsterNote"}, false, "Hunting Log")
                , (new [] {"PartyMemberList"}, false, "Party Members")
                , (new [] {"FriendList"}, false, "Friend list")
                , (new [] {"BlackList"}, false, "Black List")
                , (new [] {"SocialList"}, false, "Player Search")
                , (new [] {"Emote"}, false, "Emote")
                , (new [] {"FreeCompany"}, false, "Free Company")
                , (new [] {"SupportDesk"}, false, "Support Desk")
                , (new [] {"ConfigKeybind"}, false, "Keybinds")
                , (new [] {"_HudLayoutScreen"}, false, "HUD Layout")
                , (new [] {"Macro"}, false, "Macro")
                , (new [] {"GrandCompanySupplyList"}, false, "Grand Company Delivery")
                , (new [] {"GrandCompanyExchange"}, false, "Grand Company Shop")
                , (new [] {"MiragePrismPrismBox"}, false, "Glamour Dresser")
                , (new [] {"Currency"}, false, "Currency")
                , (new [] {"_MainCross"}, false, "Controller Main Menu")
                , (new [] {"JournalResult"}, false, "Quest Complete")
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
            
            
            _pluginInterface.ClientState.OnLogin += OnLogin;
            _pluginInterface.ClientState.OnLogout += OnLogout;

            #region Hooks, Functions and Addresses

            #endregion

            #region Excel Data

            #endregion
            
            pi.CommandManager.AddHandler(Command, new CommandInfo((_, args) =>
            {
                switch (args)
                {
                    case "toggle":
                        _config.ImGuiCompassEnable = !_config.ImGuiCompassEnable;
                        UpdateCompassVariables();
                        break;
                    case "on":
                        _config.ImGuiCompassEnable = true;
                        UpdateCompassVariables();
                        break;
                    case "off":
                        _config.ImGuiCompassEnable = false;
                        break;
                    default:
                        OnOpenConfigUi(null!, null!);
                        break;
                }
                
            })
            {
                HelpMessage = $"Open {PluginName} configuration menu. Use \"{Command} toggle|on|off\" to enable/disable.",
                ShowInHelp = true
            });
            
            DebugCtor();
#if RELEASE

            if (_pluginInterface.Reason == PluginLoadReason.Installer
                //|| _pluginInterface.ClientState.LocalPlayer is not null
            )
            {
                 OnLogin(null!, null!);
                _buildingConfigUi = true;
                _config.FreshInstall = true;
                _pluginInterface.UiBuilder.OnBuildUi += BuildConfigUi;
            }
#endif
        }
        
        private void OnLogout(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi -= OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi -= BuildConfigUi;

            _pluginInterface.UiBuilder.OnBuildUi -= BuildImGuiCompassNavi;
        }

        private void OnLogin(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi += OnOpenConfigUi;
            UpdateCompassVariables();
            _pluginInterface.UiBuilder.OnBuildUi += BuildImGuiCompassNavi;
        }

        #region UI

        private void BuildConfigUi()
        {
            var (shouldBuildConfigUi, changedConfig) = ConfigurationUi.DrawConfigUi(_config);
            if (changedConfig)
            {
                _pluginInterface.SavePluginConfig(_config);
                UpdateCompassVariables();
            }

            if (shouldBuildConfigUi) return;
            _pluginInterface.UiBuilder.OnBuildUi -= BuildConfigUi;
            _buildingConfigUi = false;
        }
        
        private void OnOpenConfigUi(object sender, EventArgs e)
        {
            _buildingConfigUi = !_buildingConfigUi;
            if(_buildingConfigUi)
                _pluginInterface.UiBuilder.OnBuildUi += BuildConfigUi;
            else
                _pluginInterface.UiBuilder.OnBuildUi -= BuildConfigUi;
            
        }

        #endregion
        
        #region Debug Partials
        
        partial void DebugCtor();
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
                _pluginInterface.ClientState.OnLogin -= OnLogin;
                _pluginInterface.ClientState.OnLogout -= OnLogout;
                _pluginInterface.CommandManager.RemoveHandler(Command);
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