using System;
using Compass.Data;
using Compass.Resources;
using Compass.UI;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Utility;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Compass;

// ReSharper disable once ClassNeverInstantiated.Global Instantiated by Dalamud
public partial class Plugin : IDalamudPlugin
{
    internal static  IPluginLog             PluginLog { get; private set; } = null!;
    public const     string                 PluginName = "Compass";
    public           string                 Name => PluginName;
    private const    string                 Command = "/compass";
    private readonly IClientState           _clientState;
    private readonly ICommandManager        _commands;
    private readonly Compass                _compass;
    private readonly Configuration          _config;
    private readonly IDalamudPluginInterface _pluginInterface;
    private          bool                   _buildingConfigUi;
    private          bool                   _isDisposed;

    public Plugin(
        IDalamudPluginInterface           pi
      , ISigScanner                       sigScanner
      , IClientState                      clientState
      , ICommandManager                   commands
      , ICondition                        condition
      , IGameGui                          gameGui
      , IPluginLog                        pluginLog
      , IGameInteropProvider              gameInteropProvider
    )
    {
        PluginLog = pluginLog;
        gameInteropProvider.InitializeFromAttributes(this);
        _pluginInterface = pi;
        _config          = GetAndMigrateConfig(pi);
        _clientState     = clientState;
        _commands        = commands;
        _compass = new Compass(
            condition,
            gameGui,
            _config
        );

        _clientState.Login  += OnLogin;
        _clientState.Logout += OnLogout;

        _commands.AddHandler(Command, new CommandInfo(CommandHandler)
        {
            HelpMessage =
                string.Format(i18n.command_help_text, PluginName, Command),
            ShowInHelp = true
        });

        DebugCtor(sigScanner);
        // TODO Need a double check for existing config, else this overrides position
        if (_pluginInterface.Reason.HasFlag(PluginLoadReason.Installer))
        {
            // NOTE: Centers compass on first install
            PluginLog.Information("Fresh install of Compass; centering compass, drawing modal.");
            var screenSizeCenterX = (ImGuiHelpers.MainViewport.Size * 0.5f).X;
            _config.ImGuiCompassPosition    =  _config.ImGuiCompassPosition with { X = screenSizeCenterX - _config.ImGuiCompassWidth * 0.5f };
            _buildingConfigUi               =  true;
            _config.FreshInstall            =  true;
            _pluginInterface.UiBuilder.Draw += DrawConfigUi;
        }

        if(_clientState.IsLoggedIn) OnLogin();

    }

    private void OnLogout(int type, int code)
    {
        _pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        _pluginInterface.UiBuilder.Draw         -= DrawConfigUi;

        _pluginInterface.UiBuilder.Draw -= _compass.Draw;
    }

    private void OnLogin()
    {
        _pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        _compass.UpdateCachedVariables();
        _pluginInterface.UiBuilder.Draw += _compass.Draw;
    }

    private void CommandHandler(string command, string args)
    {
        switch (args)
        {
            case "toggle":
                _config.ImGuiCompassEnable ^= true;
                _compass.UpdateCachedVariables();
                break;
            case "on":
                _config.ImGuiCompassEnable = true;
                _compass.UpdateCachedVariables();
                break;
            case "off":
                _config.ImGuiCompassEnable = false;
                break;
            case "togglep":
                _config.ImGuiCompassEnable ^= true;
                _compass.UpdateCachedVariables();
                _pluginInterface.SavePluginConfig(_config);
                break;
            case "onp":
                _config.ImGuiCompassEnable = true;
                _compass.UpdateCachedVariables();
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
    }

    private static Configuration GetAndMigrateConfig(IDalamudPluginInterface pi)
    {
        var config = pi.GetPluginConfig() as Configuration ?? new Configuration();
        switch (config.Version)
        {
            case 0:
            {
                PluginLog.Debug("Migrate configuration 0 to version 1:");
                PluginLog.Debug($"HideInCombat: ${config.HideInCombat}");
                config.Visibility = config.HideInCombat ? CompassVisibility.NotInCombat : CompassVisibility.Always;
                config.Version    = 1;
                pi.SavePluginConfig(config);
                break;
            }
        }

        // NOTE: Technical debt; This array is order-sensitive as it is used in combination with
        //  Configuration.ShouldHideOnUiObjectSerializer. There were problems serializing a Dictionary,
        //  but apart from that I am unsure why _this_ was the solution I came up with. Oh well.
        config.ShouldHideOnUiObject = Constant.InitialUiObjectArray;
        for (var i = 0; i < config.ShouldHideOnUiObjectSerializer.Length; i++)
        {
            config.ShouldHideOnUiObject[i].disable = config.ShouldHideOnUiObjectSerializer[i];
        }

        if (config.ShouldHideOnUiObjectSerializer.Length < config.ShouldHideOnUiObject.Length)
        {
            Array.Resize(ref config.ShouldHideOnUiObjectSerializer, config.ShouldHideOnUiObject.Length);
        }

        return config;
    }

    #region UI

    private void DrawConfigUi()
    {
        var (shouldBuildConfigUi, changedConfig) = Config.Draw(_config);
        if (changedConfig)
        {
            _pluginInterface.SavePluginConfig(_config);
            _compass.UpdateCachedVariables();
        }

        if (shouldBuildConfigUi) return;
        _pluginInterface.UiBuilder.Draw -= DrawConfigUi;
        _buildingConfigUi               =  false;
    }

    private void OnOpenConfigUi()
    {
        _buildingConfigUi = !_buildingConfigUi;
        if (_buildingConfigUi)
        {
            _pluginInterface.UiBuilder.Draw += DrawConfigUi;
        } else
        {
            _pluginInterface.UiBuilder.Draw -= DrawConfigUi;
        }
    }

    #endregion

    #region Debug Partials

    partial void DebugCtor(ISigScanner sigScanner);

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
            // Dispose managed resources
            OnLogout(0, 0);
            _clientState.Login  -= OnLogin;
            _clientState.Logout -= OnLogout;
            _commands.RemoveHandler(Command);
            DebugDtor();
        }

        // Dispose unmanaged resources
        _isDisposed = true;
    }

    ~Plugin()
    {
        Dispose(false);
    }

    #endregion
}