#if DEBUG
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;

namespace Compass;

public unsafe partial class Plugin
{
    [Signature("40 53 48 83 EC ?? B2 ?? C6 81 ?? ?? ?? ?? ?? 48 8B D9 E8 ?? ?? ?? ?? 33 D2", Fallibility = Fallibility.Fallible)]
    private readonly delegate* unmanaged<AgentMap*, void> _resetMiniMapMarkers = null!;

    private delegate nuint WriteFileHidDOutputReport(int hidDevice, DualSenseOutputReport* outputReport, ushort reportLength);

    [Signature("E8 ?? ?? ?? ?? 83 7B 18 00 74 55", DetourName = nameof(WriteFileHidDOutputReportDetour))]
    private Hook<WriteFileHidDOutputReport>? _writeFileHidDOutputReportHook { get; init; }

    [Signature("48 8D 1D ?? ?? ?? ?? 4C 8B EA", Offset = 4, UseFlags = SignatureUseFlags.Offset)]
    private int qword_140002258E20 { get; init; }

    [Signature("48 8D 05 ?? ?? ?? ?? 45 33 E4 4C 39 23", Offset = 4, UseFlags = SignatureUseFlags.Offset)]
    private int unk_140002269DE0 { get; init; }

    private int _controllerNumber = 1;

    private const byte DS_OUTPUT_VALID_FLAG0_COMPATIBLE_VIBRATION    = 1 << 0;
    private const byte DS_OUTPUT_VALID_FLAG0_HAPTICS_SELECT          = 1 << 1;
    private const byte DS_OUTPUT_VALID_FLAG0_ADAPTIVE_TRIGGER_SELECT = 1 << 2;

    private const byte DS_OUTPUT_VALID_FLAG1_MIC_MUTE_LED_CONTROL_ENABLE     = 1 << 0;
    private const byte DS_OUTPUT_VALID_FLAG1_POWER_SAVE_CONTROL_ENABLE       = 1 << 1;
    private const byte DS_OUTPUT_VALID_FLAG1_LIGHTBAR_CONTROL_ENABLE         = 1 << 2;
    private const byte DS_OUTPUT_VALID_FLAG1_RELEASE_LEDS                    = 1 << 3;
    private const byte DS_OUTPUT_VALID_FLAG1_PLAYER_INDICATOR_CONTROL_ENABLE = 1 << 4;

    private const byte DS_OUTPUT_VALID_FLAG2_LIGHTBAR_SETUP_CONTROL_ENABLE = 1 << 1;
    private const byte DS_OUTPUT_VALID_FLAG2_COMPATIBLE_VIBRATION2         = 1 << 2;
    private const byte DS_OUTPUT_POWER_SAVE_CONTROL_MIC_MUTE               = 1 << 4;
    private const byte DS_OUTPUT_LIGHTBAR_SETUP_LIGHT_OUT                  = 1 << 1;

    private nuint WriteFileHidDOutputReportDetour(int hidDevice, DualSenseOutputReport* outputReport, ushort reportLength)
    {
        var result = _writeFileHidDOutputReportHook.Original(hidDevice, outputReport, reportLength);
        PluginLog.Debug(
            $"WriteFile HidD OutputReportHook hidD:{hidDevice}:0x{hidDevice:x8} length:{reportLength}:0x{reportLength:x8} -> 0x{result:x8}");
        PluginLog.Debug($"" +
                        $"\nId: 0x{outputReport->Id:x8}" +
                        $"\nFlag0: 0x{outputReport->Flag0:x8}" +
                        $"\nFlag1: 0x{outputReport->Flag1:x8}" +
                        $"\nMotorRight: 0x{outputReport->MotorRight:x8}" +
                        $"\nMotorLeft: 0x{outputReport->MotorLeft:x8}" +
                        $"\nMicButtonLed: 0x{outputReport->MicButtonLed:x8}" +
                        $"\nPowerSaveControl: 0x{outputReport->PowerSaveControl:x8}" +
                        $"\nFlag2: 0x{outputReport->Flag2:x8}" +
                        $"\nLightBarSetup: 0x{outputReport->LightBarSetup:x8}" +
                        $"\nPlayerLedBrightness: 0x{outputReport->PlayerLedBrightness:x8}" +
                        $"\nPlayerLeds: 0x{outputReport->PlayerLeds:x8}" +
                        $"\nLightBarColourRed: 0x{outputReport->LightBarColourRed:x8}" +
                        $"\nLightBarColourGreen: 0x{outputReport->LightBarColourGreen:x8}" +
                        $"\nLightBarColourBlue: 0x{outputReport->LightBarColourBlue:x8}");
        return result;
    }

    partial void DebugCtor(ISigScanner sigScanner)
    {
        _commands.AddHandler($"{Command}debug", new CommandInfo((_, _) =>
        {
            _pluginInterface.UiBuilder.Draw -= BuildDebugUi;
            _pluginInterface.UiBuilder.Draw += BuildDebugUi;
        })
        {
            HelpMessage = $"Open {PluginName} Debug menu.", ShowInHelp = false
        });


        _pluginInterface.UiBuilder.Draw += BuildDebugUi;
        if (_writeFileHidDOutputReportHook is not null)
        {
            PluginLog.Warning($"Hook enabled? {_writeFileHidDOutputReportHook.IsEnabled}");
            _writeFileHidDOutputReportHook.Enable();
        } else
        {
            PluginLog.Warning("Hook not set!");
        }
    }


    private unsafe void BuildDebugUi()
    {
        ImGui.SetNextWindowBgAlpha(1);
        if (!ImGui.Begin($"{PluginName} Debug"))
        {
            ImGui.End();
            return;
        }

        var agentMap = AgentMap.Instance();
        ImGui.Text(
            $"{nameof(qword_140002258E20)}:0x{qword_140002258E20:x8}\n{nameof(unk_140002269DE0)}:0x{unk_140002269DE0:x8}");

        ImGui.Text($"1 << 1 0x{(byte)(1 << 1):x8}\n1 << 0 0x{(byte)(1 << 0):x8}");
        ImGui.InputInt("Controller Number", ref _controllerNumber);
        if (ImGui.Button("Reset All"))
        {
            var report = stackalloc DualSenseOutputReport[1];
            report->Id    = 0x2; // USB
            report->Flag0 = DS_OUTPUT_VALID_FLAG0_ADAPTIVE_TRIGGER_SELECT;
            report->Flag1 = DS_OUTPUT_VALID_FLAG1_LIGHTBAR_CONTROL_ENABLE | DS_OUTPUT_VALID_FLAG1_MIC_MUTE_LED_CONTROL_ENABLE |
                            DS_OUTPUT_VALID_FLAG1_PLAYER_INDICATOR_CONTROL_ENABLE;
            // report->Flag1               = 0xF7;
            report->MotorRight          = 0x00;
            report->MotorLeft           = 0x0;
            report->MicButtonLed        = 0x0;
            report->Flag2               = 0x0;
            report->LightBarSetup       = 0x2;
            report->PlayerLedBrightness = 0x2;
            report->PlayerLeds          = 0x0;
            report->LightBarColourRed   = 0xFF;
            report->LightBarColourGreen = 0x00;
            report->LightBarColourBlue  = 0x00;
            var result = _writeFileHidDOutputReportHook.Original(_controllerNumber, report, 0x30);
            PluginLog.Debug($"WriteFile HidD OutputReportHook result: 0x{result:x8}");
        }

        if (ImGui.Button("Rumble it"))
        {
            var report = stackalloc DualSenseOutputReport[1];
            report->Id = 0x2; // USB
            // report->Flag0 = 0xFF;
            // report->Flag1 = 0xF7;
            report->Flag0 = DS_OUTPUT_VALID_FLAG0_HAPTICS_SELECT | DS_OUTPUT_VALID_FLAG0_ADAPTIVE_TRIGGER_SELECT;
            report->Flag1 = DS_OUTPUT_VALID_FLAG1_LIGHTBAR_CONTROL_ENABLE | DS_OUTPUT_VALID_FLAG1_MIC_MUTE_LED_CONTROL_ENABLE |
                            DS_OUTPUT_VALID_FLAG1_PLAYER_INDICATOR_CONTROL_ENABLE;
            report->MotorRight          = 0xFF;
            report->MotorLeft           = 0x0;
            report->MicButtonLed        = 0x2;
            report->Flag2               = DS_OUTPUT_VALID_FLAG2_COMPATIBLE_VIBRATION2;
            report->LightBarSetup       = 0x2;
            report->PlayerLedBrightness = 0x0;
            report->PlayerLeds          = 0x20 | 0x08 | 0x02;
            report->LightBarColourRed   = 0x00;
            report->LightBarColourGreen = 0xFF;
            report->LightBarColourBlue  = 0xFF;
            report->TriggerR2[0]        = 0x26;
            report->TriggerR2[1]        = 0xFF;
            report->TriggerR2[2]        = 0x00;
            report->TriggerR2[4]        = (byte)(0xFF * 0.7);
            report->TriggerR2[5]        = (byte)(0xFF * 0.8);
            report->TriggerR2[6]        = (byte)(0xFF * 0.9);
            report->TriggerR2[9]        = (byte)(0xFF * 0.1);
            var result = _writeFileHidDOutputReportHook.Original(_controllerNumber, report, 0x30);
            PluginLog.Debug($"WriteFile HidD OutputReportHook result: 0x{result:x8}");
        }


        ImGui.End();
    }

    partial void DebugDtor()
    {
        _pluginInterface.UiBuilder.Draw -= BuildDebugUi;
        _commands.RemoveHandler($"{Command}debug");
        _writeFileHidDOutputReportHook?.Disable();
        _writeFileHidDOutputReportHook?.Dispose();
    }
}

// https://github.com/torvalds/linux/blob/master/drivers/hid/hid-playstation.c#L227
[StructLayout(LayoutKind.Explicit, Size = 0x2F)]
public unsafe struct DualSenseOutputReport
{
    [FieldOffset(0x00)] public byte Id; // USB 0x02, BT ignored, different header and headache
    [FieldOffset(0x01)] public byte Flag0;
    [FieldOffset(0x02)] public byte Flag1;

    [FieldOffset(0x03)] public       byte MotorRight;
    [FieldOffset(0x04)] public       byte MotorLeft;
    [FieldOffset(0x05)] public fixed byte Reserved[4];
    [FieldOffset(0x09)] public       byte MicButtonLed;
    [FieldOffset(0x0A)] public       byte PowerSaveControl;

    [FieldOffset(0x0B)] public fixed byte TriggerR2[10];
    [FieldOffset(0x15)] public fixed byte TriggerL2[10];
    [FieldOffset(0x1F)] public fixed byte Reserved2[8];

    [FieldOffset(0x27)] public       byte Flag2; // LightBar?
    [FieldOffset(0x28)] public fixed byte Reserved3[2];
    [FieldOffset(0x2A)] public       byte LightBarSetup;

    [FieldOffset(0x2B)] public byte PlayerLedBrightness;
    [FieldOffset(0x2C)] public byte PlayerLeds;

    [FieldOffset(0x2D)] public byte LightBarColourRed;
    [FieldOffset(0x2E)] public byte LightBarColourGreen;
    [FieldOffset(0x2F)] public byte LightBarColourBlue;
}

#endif