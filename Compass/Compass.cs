using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Compass.Collection;
using Compass.Interop;
using Dalamud.Data.LuminaExtensions;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;
using Vector2 = System.Numerics.Vector2;
using static Compass.Extensions;

// TODO 5 Refactor DrawCombo to generic
namespace Compass
{
    public partial class Compass : IDisposable
    {
        private const string Command = "/compass";
        public const string PluginName = "Compass";

        
        
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly Configuration _config;

        private delegate float ClampToMinusPiAndPiDelegate(float degree);

        private delegate void SetCameraRotationDelegate(nint cameraThis, float degree);
        
        private readonly Hook<ClampToMinusPiAndPiDelegate> _clampToMinusPiAndPi;
        private readonly Hook<SetCameraRotationDelegate> _setCameraRotation;
        private bool _buildingConfigUi;
        private bool _isDisposed;
        private float _calledWithDegree;
        private float _setToDegree;
        private float _calledWithDegreeFromSetCameraRotation;
        private nint _maybeCameraStruct;
        private nint _sceneCameraObject;
        private nint _gameCameraObject;
        private nint _cameraBase;
        private nint _cameraManager;
        private bool _shouldUpdate;
        private nint _unknown;
        private TextureWrap _compassImage;
        private readonly Stopwatch _stopwatch = new();
        private long _logTicks = Stopwatch.Frequency * 2;


        private void SetCameraRotation(nint cameraThis, float degree)
        {
            _shouldUpdate = true;
            _setCameraRotation.Original(cameraThis, degree);
            _shouldUpdate = false;
            _maybeCameraStruct = cameraThis;
            _calledWithDegreeFromSetCameraRotation = degree;
            
        } 
        private float ClampToMinusPiAndPi(float degree)
        {
            var original = _clampToMinusPiAndPi.Original(degree);
            if (!_shouldUpdate) return original;
            _calledWithDegree = degree;
            _setToDegree = original;

            return original;
        }

        public Compass(DalamudPluginInterface pi, Configuration config)
        {
            #region Signatures

            const string clampToMinusPiAndPiSignature = "F3 0F ?? ?? ?? ?? ?? ?? 0F 57 ?? 0F 2F ?? 0F 28 ?? 72";
            const string setCameraRotationSignature = "40 ?? 48 83 EC ?? 0F 2F ?? ?? ?? ?? ?? 48 8B";
            const string sceneCameraCtorSig = "E8 ?? ?? ?? ?? 4C 8B E0 49 8B CC"; 
            const string gameCameraCtorSig = "88 83 ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 48 89 03 C6 83 ?? ?? ?? ?? ?? "; 
            const string cameraBaseSig = "48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 48 83 C1 10 E8 ?? ?? ?? ?? 33 C0"; 
            const string cameraManagerSignature = "48 8B 05 ?? ?? ?? ?? 48 89 4C D0 ??";
            #endregion


            _pluginInterface = pi;
            _config = config;
            
            _pluginInterface.ClientState.OnLogin += OnLogin;
            _pluginInterface.ClientState.OnLogout += OnLogout;

            #region Hooks, Functions and Addresses
            

            _clampToMinusPiAndPi = new Hook<ClampToMinusPiAndPiDelegate>(
                _pluginInterface.TargetModuleScanner.ScanText(clampToMinusPiAndPiSignature),
                (ClampToMinusPiAndPiDelegate) ClampToMinusPiAndPi );
            _clampToMinusPiAndPi.Enable();
            
            _setCameraRotation = new Hook<SetCameraRotationDelegate>(
                _pluginInterface.TargetModuleScanner.ScanText(setCameraRotationSignature),
                (SetCameraRotationDelegate) SetCameraRotation );
            _setCameraRotation.Enable();

            _sceneCameraObject = _pluginInterface.TargetModuleScanner.GetStaticAddressFromSig(sceneCameraCtorSig, 0xC);
            _gameCameraObject = _pluginInterface.TargetModuleScanner.GetStaticAddressFromSig(gameCameraCtorSig);
            _cameraBase = _pluginInterface.TargetModuleScanner.GetStaticAddressFromSig(cameraBaseSig);
            _cameraManager = _pluginInterface.TargetModuleScanner.GetStaticAddressFromSig(cameraManagerSignature);

            #endregion

            #region Excel Data
            
            #endregion
            
            _stopwatch.Start();
            var imagePath = Path.Combine(Path.GetDirectoryName(CompassBridge.AssemblyLocation)!, @"res/compass.png");
            _compassImage = _pluginInterface.UiBuilder.LoadImage(imagePath);
            pi.CommandManager.AddHandler(Command, new CommandInfo((_, _) => { OnOpenConfigUi(null!, null!); })
            {
                HelpMessage = $"Open {PluginName} configuration menu.",
                ShowInHelp = true
            });
            
#if DEBUG
            pi.CommandManager.AddHandler($"{Command}debug", new CommandInfo((_, _) =>
            {
                _pluginInterface.UiBuilder.OnBuildUi -= BuildDebugUi;
                _pluginInterface.UiBuilder.OnBuildUi += BuildDebugUi;
            })
            {
                HelpMessage = $"Open {PluginName} Debug menu.",
                ShowInHelp = false
            });
            if (_pluginInterface.ClientState.LocalPlayer is not null)
            {
                OnLogin(null!, null!);
                _pluginInterface.UiBuilder.OnBuildUi += BuildDebugUi;
                _buildingConfigUi = true;
            }
#else
            
            if(_pluginInterface.Reason == PluginLoadReason.Installer 
            //   || _pluginInterface.ClientState.LocalPlayer is not null
               )
            {
                OnLogin(null!, null!);
            }
#endif
            
        }
        

        private void OnLogout(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi -= OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi -= BuildUi;
            
            
        }

        private void OnLogin(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi += OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi += BuildUi;
        }



        private void ThrottledLog(string msg)
        {
            if (_stopwatch.ElapsedTicks <= _logTicks) return;
            PluginLog.Log(msg);
            _stopwatch.Restart();
        }
        

        


        // Compass calculations modified from https://github.com/gw2bgdm/gw2bgdm/blob/master/src/meter/imgui_bgdm.cpp:2392 
        
        private unsafe void BuildCompass()
        {
            var areaMap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("AreaMap", 1);
            if (areaMap->IsVisible) return;
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
                                               | ImGuiWindowFlags.NoMove
                                               | ImGuiWindowFlags.NoMouseInputs
                                               | ImGuiWindowFlags.NoFocusOnAppearing
                                               | ImGuiWindowFlags.NoBackground
                                               | ImGuiWindowFlags.NoNav
                                               | ImGuiWindowFlags.NoInputs
                                               | ImGuiWindowFlags.NoCollapse;
            
            //ImGui.SetNextWindowSize(new Vector2(_compassImage.Width+5, _compassImage.Height+5), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.3f);
            ImGui.SetNextWindowSizeConstraints(new Vector2(250f,50f), new Vector2(int.MaxValue,50f));
            if (!ImGui.Begin("Compass##TheRealCompass"
                , _buildingConfigUi 
                    ? ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar
                    : flags)
            )
            { ImGui.End(); return; }
            
            var cameraRotationInRadian = -*(float*) (_maybeCameraStruct + 0x130);
            var cameraRotationInDegree = (cameraRotationInRadian) * Rad2Deg;
            
            
            /*
            ImGui.Image(
                _compassImage.ImGuiHandle,
                new Vector2(_compassImage.Width, _compassImage.Height),
                new Vector2(-1f + (float)(cameraRotationInRadian / Math.PI/2),0f),
                    new Vector2((float)(cameraRotationInRadian / Math.PI/2),1)
                );
            */
            var naviMap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);

            if (naviMap->ULDData.LoadedState != 3)
            {
                ImGui.End();
            }

            try
            {
                for (int i = 9; i < 13; i++)
                {
                    var imgNode = (AtkImageNode*) naviMap->ULDData.NodeList[i];
                    var part = imgNode->PartsList->Parts[imgNode->PartId];
                    var type = part.ULDTexture->AtkTexture.TextureType;
                    if (type != TextureType.Resource) continue;
                    var texFileNamePtr =
                        part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                            .FileName;
                    var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                    //ImGui.Text($"Texture Path {texString}");


                    //ThrottledLog($"{i} Drawing {texString}");
                    var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                    var u = (float) part.U / tex->Width;
                    var v = (float) part.V / tex->Height;
                    var u1 = (float) (part.U + part.Width) / tex->Width;
                    var v1 = (float) (part.V + part.Height) / tex->Height;

                    var playerX = 72;
                    var playerY = 72;
                    var cos = (float) Math.Cos(cameraRotationInRadian);
                    var sin = (float) Math.Sin(cameraRotationInRadian);
                    var playerViewVector2 = new Vector2(-sin, cos);
                    var widthOfCompass = _compassImage.Width;
                    widthOfCompass = (int) ImGui.GetWindowContentRegionWidth();
                    var compassUnit = widthOfCompass / 360f;
                    var x = i switch
                    {
                        9 => -1,
                        10 => 0,
                        11 => 1,
                        _ => 0
                    };
                    var y = i switch
                    {
                        9 => 0,
                        10 => -1,
                        11 => 0,
                        _ => 1
                    };

                    var toObject = new Vector2(x, y);
                    var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                    //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                    var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                    ImGui.SameLine(widthOfCompass / 2f + compassPos.X);
                    ImGui.Image(
                        new IntPtr(tex->D3D11ShaderResourceView),
                        imgNode->PartId == 0
                            ? new Vector2(tex->Width, tex->Height) * ImGui.GetIO().FontGlobalScale
                            : new Vector2(part.Width, part.Height) * ImGui.GetIO().FontGlobalScale
                        , imgNode->PartId == 0 ? new Vector2(0, 0) : new Vector2(u, v)
                        , imgNode->PartId == 0 ? new Vector2(1, 1) : new Vector2(u1, v1)


                    );


                }

                var miniMapRootComponentNode = (AtkComponentNode*) naviMap->ULDData.NodeList[2];
                for (var i = 4; i < miniMapRootComponentNode->Component->ULDData.NodeListCount; i++)
                {
                    var baseComponentNode =
                        (AtkComponentNode*) miniMapRootComponentNode->Component->ULDData.NodeList[i];
                    if (!baseComponentNode->AtkResNode.IsVisible) continue;
                    for (var j = 2; j < baseComponentNode->Component->ULDData.NodeListCount; j++)
                    {
                        var node = baseComponentNode->Component->ULDData.NodeList[j];
                        if (!node->IsVisible || node->Type != NodeType.Image) continue;
                        var imgNode = (AtkImageNode*) node;
                        var part = imgNode->PartsList->Parts[imgNode->PartId];
                        var type = part.ULDTexture->AtkTexture.TextureType;
                        if (type != TextureType.Resource) continue;
                        var texFileNamePtr =
                            part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                        //ImGui.Text($"Texture Path {texString}");
                        if (
                            (texString!.EndsWith("060443.tex")) // Player Marker
                            //(texString!.EndsWith("060955.tex")) // Arrow Down on QuestMarker
                            //|| (texString!.EndsWith("071025.tex") ) // Quest Complete Marker
                            //|| (texString!.EndsWith("060954.tex") )
                        ) // Arrow UP on QuestMarker
                        {
                            //ThrottledLog($"{i} Drawing {texString}");
                            var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                            var u = (float) part.U / tex->Width;
                            var v = (float) part.V / tex->Height;
                            var u1 = (float) (part.U + part.Width) / tex->Width;
                            var v1 = (float) (part.V + part.Height) / tex->Height;
                            var cosArrow = (float) Math.Cos(
                                baseComponentNode->AtkResNode.Rotation //+ imgNode->AtkResNode.Rotation
                            );
                            var sinArrow = (float) Math.Sin(
                                baseComponentNode->AtkResNode.Rotation //+ imgNode->AtkResNode.Rotation
                            );
                            var playerX = 72;
                            var playerY = 72;
                            var cos = (float) Math.Cos(cameraRotationInRadian);
                            var sin = (float) Math.Sin(cameraRotationInRadian);
                            var playerViewVector2 = new Vector2(-sin, cos);
                            var widthOfCompass = _compassImage.Width;
                            widthOfCompass = (int) ImGui.GetWindowContentRegionWidth();
                            var compassUnit = widthOfCompass / 360f;
                            var x = baseComponentNode->AtkResNode.X;
                            var y = baseComponentNode->AtkResNode.Y;
                            var toObject = new Vector2(playerX - x, playerY - y);
                            var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                            //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                            var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                            ImGui.SameLine(widthOfCompass / 2f + compassPos.X);
                            ImGui.Image(
                                new IntPtr(tex->D3D11ShaderResourceView),
                                imgNode->PartId == 0
                                    ? new Vector2(tex->Width, tex->Height) * ImGui.GetIO().FontGlobalScale
                                    : new Vector2(part.Width, part.Height) * ImGui.GetIO().FontGlobalScale
                                , imgNode->PartId == 0 ? new Vector2(0, 0) : new Vector2(u, v)
                                , imgNode->PartId == 0 ? new Vector2(1, 1) : new Vector2(u1, v1)


                            );
                        }
                        
                        else if (
                            ((texString?.EndsWith("navimap.tex") ?? false) && (imgNode->PartId == 18 || imgNode->PartId == 20))

                        )
                        {
                            //PluginLog.Log($"Drawing for {texString}");
                            var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                            var u = (float) part.U / tex->Width;
                            var v = (float) part.V / tex->Height;
                            var u1 = (float) (part.U + part.Width) / tex->Width;
                            var v1 = (float) (part.V + part.Height) / tex->Height;
                            var cosArrow = (float) Math.Cos(baseComponentNode->AtkResNode.Rotation +
                                                            imgNode->AtkResNode.Rotation);
                            var sinArrow = (float) Math.Sin(baseComponentNode->AtkResNode.Rotation +
                                                            imgNode->AtkResNode.Rotation);
                            var playerX = 72;
                            var playerY = 72;
                            var cos = (float) Math.Cos(cameraRotationInRadian);
                            var sin = (float) Math.Sin(cameraRotationInRadian);
                            var playerViewVector2 = new Vector2(-sin, cos);
                            var widthOfCompass = _compassImage.Width;
                            widthOfCompass = (int) ImGui.GetWindowContentRegionWidth();
                            var compassUnit = widthOfCompass / 360f;
                            var toObject = new Vector2(-sinArrow, cosArrow);
                            var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                            //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                            var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                            ImGui.SameLine(widthOfCompass / 2f + compassPos.X);
                            ImGui.Image(
                                new IntPtr(tex->D3D11ShaderResourceView),
                                imgNode->PartId == 0
                                    ? new Vector2(tex->Width, tex->Height) * ImGui.GetIO().FontGlobalScale
                                    : new Vector2(part.Width, part.Height) * ImGui.GetIO().FontGlobalScale
                                , imgNode->PartId == 0 ? new Vector2(0, 0) : new Vector2(u, v)
                                , imgNode->PartId == 0 ? new Vector2(1, 1) : new Vector2(u1, v1)


                            );
                        }
                        else if (!texString?.EndsWith("navimap.tex") ?? true)
                        {
                            // PluginLog.Log($"Drawing for {texString}");                                    
                            var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                            var u = (float) part.U / tex->Width;
                            var v = (float) part.V / tex->Height;
                            var u1 = (float) (part.U + part.Width) / tex->Width;
                            var v1 = (float) (part.V + part.Height) / tex->Height;
                            var x = baseComponentNode->AtkResNode.X;
                            var y = baseComponentNode->AtkResNode.Y;
                            var cosArrow = (float) Math.Cos(
                                baseComponentNode->AtkResNode.Rotation //+ imgNode->AtkResNode.Rotation
                            );
                            var sinArrow = (float) Math.Sin(
                                baseComponentNode->AtkResNode.Rotation //+ imgNode->AtkResNode.Rotation
                            );
                            var playerX = 72;
                            var playerY = 72;
                            var cos = (float) Math.Cos(cameraRotationInRadian);
                            var sin = (float) Math.Sin(cameraRotationInRadian);
                            var playerViewVector =
                                Vector2.UnitY.Rotate(
                                    cos,
                                    sin);
                            var dot = Vector2.Dot(Vector2.Normalize(new Vector2(playerX - x, playerY - y)),
                                playerViewVector);
                            var playerViewVector2 = new Vector2(-sin, cos);
                            var widthOfCompass = _compassImage.Width;
                            widthOfCompass = (int) ImGui.GetWindowContentRegionWidth();
                            var compassUnit = widthOfCompass / 360f;
                            var toObject = baseComponentNode->AtkResNode.Rotation == 0
                                ? new Vector2(playerX - x, playerY - y)
                                : new Vector2(-sinArrow, cosArrow);


                            var signedAngle = ((float) Math.Atan2(toObject.Y, toObject.X) -
                                               (float) Math.Atan2(playerViewVector.Y, playerViewVector.X));
                            var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                            //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                            var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                            ImGui.SameLine(widthOfCompass / 2f + compassPos.X);
                            ImGui.Image(
                                new IntPtr(tex->D3D11ShaderResourceView),
                                imgNode->PartId == 0
                                    ? new Vector2(tex->Width, tex->Height) * ImGui.GetIO().FontGlobalScale
                                    : new Vector2(part.Width, part.Height) * ImGui.GetIO().FontGlobalScale
                                , imgNode->PartId == 0 ? new Vector2(0, 0) : new Vector2(u, v)
                                , imgNode->PartId == 0 ? new Vector2(1, 1) : new Vector2(u1, v1)


                            );
                            /*
                            ImGui.Text($"Found IT {x} {y}");
                            ImGui.Text($"PlayerViewVector {playerViewVector}");
                            ImGui.Text($"PlayerViewVector2 {playerViewVector2}");
                            ImGui.Text($"Dot {dot}");
                            ImGui.Text($"Compass Unit {compassUnit}");
                            ImGui.Text($"Signed Angle: {signedAngle}/{signedAngle * Rad2Deg}");
                            ImGui.Text($"Signed Angle2: {signedAngle2 * Deg2Rad}/{signedAngle2}");
                            ImGui.Text($"CompassPos: {compassPos}");
                            */

                        }

                        //ImGui.Text($"{imgNode->AtkResNode.NodeID}");
                    }
                }
            }
            catch(Exception e)
            {
                PluginLog.Error(e, "Caught Exception in the while loop.");
            }


            ImGui.End();
        }

        #region UI

        private void BuildUi()
        {
            BuildCompass();
            if (!_buildingConfigUi) return;
            var (shouldBuildConfigUi, changedConfig) = ConfigurationUi.DrawConfigUi(_config);
            if (changedConfig) _pluginInterface.SavePluginConfig(_config);
            if (!shouldBuildConfigUi) _buildingConfigUi = false;

        }


        private void OnOpenConfigUi(object sender, EventArgs e)
        {
            _buildingConfigUi = !_buildingConfigUi;

        }

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
                
                _clampToMinusPiAndPi?.Disable();
                _clampToMinusPiAndPi?.Dispose();
                _setCameraRotation?.Disable();
                _setCameraRotation?.Dispose();
                
                _compassImage?.Dispose();
#if  DEBUG
                _pluginInterface.UiBuilder.OnBuildUi -= BuildDebugUi;
                _pluginInterface.CommandManager.RemoveHandler($"{Command}debug");
#endif
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