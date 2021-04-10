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
using FFXIVClientStructs;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.Helper;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;
using Vector2 = System.Numerics.Vector2;
using static Compass.Extensions;

// TODO 5 Refactor DrawCombo to generic
namespace Compass
{
    public unsafe partial class Compass : IDisposable
    {
        private const string Command = "/compass";
        public const string PluginName = "Compass";

        public Configuration PluginConfig => _config;
        public DalamudPluginInterface PluginInterface => _pluginInterface;
        
        
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly Configuration _config;
        
        

        private delegate float ClampToMinusPiAndPiDelegate(float degree);

        private delegate void SetCameraRotationDelegate(nint cameraThis, float degree);


        private delegate long AtkUnitBase_SetPosition(nint thisUnitBase, short x, short y);

        private delegate void AtkResNode_SetPositionShort(nint thisRedNode, short x, short y);
        private delegate void AtkResNode_SetPositionFloat(nint thisRedNode, float x, float y);

        // NOTE This is Maybe Component::GUI::AtkUnitManager/Client::UI::RaptureAtkUnitManager +0x28
        // OR NOT
        // Component::GUI::AtkComponentBase +0x8 maybe?
        private delegate nint CreateAtkNode(nint thisUnused, NodeType type);

        private delegate nint AtkImageNode_Destroy(nint thisatkImageNode, bool freeMemory);
        private AtkResNode_SetPositionFloat _atkResNodeSetPositionFloat;
        private AtkResNode_SetPositionShort _atkResNodeSetPositionShort;
        private AtkUnitBase_SetPosition _atkUnitBaseSetPosition;
        private CreateAtkNode _createAtkNode;
        private AtkImageNode_Destroy _destroyAtkImageNode;
        private readonly Hook<AtkResNode_SetPositionFloat> _resNodePositionFloatHook;
        private readonly Hook<AtkResNode_SetPositionShort> _resNodePositionShortHook;
        private readonly Hook<AtkUnitBase_SetPosition> _unitBaseSetPositionHook;
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
        private TextureWrap _naviMap;
        private AtkTextNode* _clonedTxtNode;
        private AtkUnitBase* _clonedUnitBase;

        private List<nint> _imageNodes = new(150);
        //NOTE (Chiv) 202 Component Nodes for the Icons on the minimap + 1*4 pointers for the Cardinals
        // Actually, there are 101*3 and 101*4 Component nodes but this it easier to deal with +
        // one can abuse the first 101,4 for something else
        // like [1,3] == backgroundNode
        private AtkImageNode*[,] _clonedImageNodes = new AtkImageNode*[202,4];
        private AtkImageNode*[] _cardinalsClonedImageNodes = new AtkImageNode*[4];
        private AtkImageNode* _background;
        private Vector2 _compassOffset = new(-360,150);
        private float _scale = 1;
        private bool _reset;
        private bool _dirty;


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
            const string atkUnitBaseSetPositionSignature = "4C 8B 89 ?? ?? ?? ?? 41 0F BF C0 ";
            const string atkResNodeSetPositionShortSignature = "E8 ?? ?? ?? ?? 49 FF CC";
            const string atkResNodeSetPositionFloatSignature = "E8 ?? ?? ?? ?? 8B 45 B8 ";
            const string createAtkNodeSignature = "E8 ?? ?? ?? ?? 48 8B 4C 24 ?? 48 8B 51 08";
            // NOTE All Destroys are basically identical, almost only the size of FreeMemory() changes.
            const string atkImageNodeDestroySignature =
                "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8D 05 ?? ?? ?? ?? 48 8B F1 48 89 01 8B FA 48 83 C1 18 E8 ?? ?? ?? ?? 48 8D 4E 18 E8 ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 48 89 06 40 F6 C7 01 74 0D BA ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 48 8B 5C 24 ?? 48 8B C6 48 8B 74 24 ?? 48 83 C4 20 5F C3 40 53 48 83 EC 20 48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 F6 C2 01 74 0A BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 20 5B C3 CC CC CC CC CC 48 89 5C 24 ??";
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


            _atkUnitBaseSetPosition = Marshal.GetDelegateForFunctionPointer<AtkUnitBase_SetPosition>(
                _pluginInterface.TargetModuleScanner.ScanText(atkUnitBaseSetPositionSignature));
            _atkResNodeSetPositionShort = Marshal.GetDelegateForFunctionPointer<AtkResNode_SetPositionShort>(
                _pluginInterface.TargetModuleScanner.ScanText(atkResNodeSetPositionShortSignature));
            _atkResNodeSetPositionFloat = Marshal.GetDelegateForFunctionPointer<AtkResNode_SetPositionFloat>(
                _pluginInterface.TargetModuleScanner.ScanText(atkResNodeSetPositionFloatSignature));
            _createAtkNode = Marshal.GetDelegateForFunctionPointer<CreateAtkNode>(
                _pluginInterface.TargetModuleScanner.ScanText(createAtkNodeSignature));
            _destroyAtkImageNode = Marshal.GetDelegateForFunctionPointer<AtkImageNode_Destroy>(
                _pluginInterface.TargetModuleScanner.ScanText(atkImageNodeDestroySignature));
            #endregion

            #region Excel Data
            
            #endregion
            
            _stopwatch.Start();
            var naviMap = _pluginInterface.Data.GetFile<TexFile>("ui/uld/NaviMap.tex");
            _naviMap = _pluginInterface.UiBuilder.LoadImageRaw(naviMap.GetRgbaImageData(), naviMap.Header.Width,
                naviMap.Header.Height, 4);
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

            _UIDebug = new UIDebug(this);
            UiHelper.Setup(_pluginInterface.TargetModuleScanner);
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
            //_pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            //_pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdateSetupAddonNodes;
            //_pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdateUpdateAddonCompass;
            
        }

        private void OnLogin(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi += OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi += BuildUi;
            //_pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            //_pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdateSetupAddonNodes;
        }

        private void BuildImGuiCompass()
        {
            // TODO (Chiv) Why use this and not the rotation on the minimap rotation thingy?
            // Minimap rotation thingy is even already flipped!
            // And apparently even accessible & updated if _NaviMap is disabled
            
            var naviMapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (naviMapPtr == IntPtr.Zero) return;
            var naviMap = (AtkUnitBase*) naviMapPtr;
            //NOTE (chiv) 3 means fully loaded
            if (naviMap->ULDData.LoadedState != 3) return;
            // TODO (chiv) Check the flag if _NaviMap is hidden in the HUD
            if (!naviMap->IsVisible) return;
            if (ShouldHideCompass()) return;
            var scale = _config.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
            const float compassHeight = 50f;
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
                                           | ImGuiWindowFlags.NoMove
                                           | ImGuiWindowFlags.NoMouseInputs
                                           | ImGuiWindowFlags.NoFocusOnAppearing
                                           | ImGuiWindowFlags.NoBackground
                                           | ImGuiWindowFlags.NoNav
                                           | ImGuiWindowFlags.NoInputs
                                           | ImGuiWindowFlags.NoCollapse;
            ImGui.SetNextWindowBgAlpha(0.3f);
            ImGui.SetNextWindowSizeConstraints(
                new Vector2(250f,(compassHeight+20) * scale), 
                new Vector2(int.MaxValue,(compassHeight+20) *scale));
            if (!ImGui.Begin("###ImGuiCompassWindow"
                , _buildingConfigUi 
                    ? ImGuiWindowFlags.NoCollapse 
                      | ImGuiWindowFlags.NoTitleBar 
                      | ImGuiWindowFlags.NoFocusOnAppearing
                      | ImGuiWindowFlags.NoScrollbar
                    : flags)
            ) { ImGui.End(); return; }
#if DEBUG
            _areaCircle.Clear();
#endif
            // NOTE (Chiv) This is the position of the player in the minimap coordinate system
            const int playerX = 72, playerY = 72;
            var cameraRotationInRadian = -*(float*) (_maybeCameraStruct + 0x130);
            var miniMapIconsRootComponentNode = (AtkComponentNode*) naviMap->ULDData.NodeList[2];
            // This leads to jerky behaviour
            //var cameraRotationInRadian = miniMapIconsRootComponentNode->Component->ULDData.NodeList[2]->Rotation;
            //var scaleFactorForRotationBasedDistance = Math.Max(1f - 0 / maxDistance, lowestScaleFactor) * ImGui.GetIO().FontGlobalScale;
            var scaleFactorForRotationBasedDistance = ImGui.GetIO().FontGlobalScale * 0.7f; 
            // TODO (Chiv) My math must be bogus somewhere, cause I need to do some things differently then math would say
            // TODO I think my and the games coordinate system do not agree
            // TODO (Chiv) Redo the math for not locked _NaviMap (might be easier? Should be the same?)
            var playerCos = (float) Math.Cos(cameraRotationInRadian);
            var playerSin = (float) Math.Sin(cameraRotationInRadian);
            //TODO (Chiv) Uhm, actually with H=1, it should be new Vector(cos,sin); that breaks calculations though...
            // Is my Coordinate System the same as the games' minimap?
            var playerForward = new Vector2(-playerSin, playerCos);
            var zeroVec = Vector2.Zero;
            var oneVec = Vector2.One;
            // TODO (Chiv) do it all in Radians
            var widthOfCompass = ImGui.GetWindowContentRegionWidth();
            var halfWidthOfCompass = widthOfCompass / 2f;
            var compassUnit = widthOfCompass / 360f;
            var westCardinalAtkImageNode = (AtkImageNode*) naviMap->ULDData.NodeList[11];
            // TODO (Chiv) Cache on TerritoryChange/Initialisation?
            var naviMapTextureD3D11ShaderResourceView = new IntPtr(
                westCardinalAtkImageNode->PartsList->Parts[0]
                        .ULDTexture->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
                );
            var drawList = ImGui.GetWindowDrawList();
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            var cursorPosition = ImGui.GetCursorScreenPos() - new Vector2(7,0);
            var compassCentre = new Vector2(cursorPosition.X + halfWidthOfCompass, cursorPosition.Y);
            // TODO (Chiv) Draw Background
            //First, the background
            backgroundDrawList.AddRectFilled(
                cursorPosition
                ,new Vector2(cursorPosition.X + widthOfCompass+13, cursorPosition.Y + compassHeight * scale)
                , ImGui.ColorConvertFloat4ToU32(new Vector4(1f,1f,0.2f,0.2f))
                , 10f
                );
            // Second, we position our Cardinals
            //TODO (Chiv) Uhm, no, east is the other way. Again, coordinate system mismatch?
            var east = -Vector2.UnitX;
            var south = -Vector2.UnitY;
            var west = Vector2.UnitX;
            var north = Vector2.UnitY;
            // TODO (Chiv) Yeah, the minus  here is bogus as hell too.
            // TODO (chiv) actually, SignedAngle first arg is FROM, not TO
            // TODO (Chiv) Draw it all manually via the Drawlist, no need for same line
            var eastOffset = compassUnit * -SignedAngle(east, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                ,new Vector2(compassCentre.X + eastOffset, compassCentre.Y)
                ,new Vector2(compassCentre.X + eastOffset + 20 * scale, compassCentre.Y + 32 *scale)
                ,new Vector2(0.5446429f, 0.8301887f)
                ,new Vector2(0.5892857f, 0.9811321f)
            );
            var southOffset = compassUnit * -SignedAngle(south, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                ,new Vector2(compassCentre.X + southOffset, compassCentre.Y)
                ,new Vector2(compassCentre.X + southOffset + 20 * scale, compassCentre.Y + 32 *scale)
                ,new Vector2(0.5892857f, 0.8301887f)
                ,new Vector2(0.6339286f, 0.9811321f)
            );
            var westOffset = compassUnit * -SignedAngle(west, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                ,new Vector2(compassCentre.X + westOffset, compassCentre.Y)
                ,new Vector2(compassCentre.X + westOffset + 32 * scale, compassCentre.Y + 32 *scale)
                ,new Vector2(0.4732143f, 0.8301887f)
                ,new Vector2(0.5446429f, 0.9811321f)
            );
            var northOffset = compassUnit * -SignedAngle(north, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                ,new Vector2(compassCentre.X + northOffset, compassCentre.Y)
                ,new Vector2(compassCentre.X + northOffset + 32 * scale, compassCentre.Y + 32 *scale)
                ,new Vector2(0.4017857f, 0.8301887f)
                ,new Vector2(0.4732143f, 0.9811321f)
                , ImGui.ColorConvertFloat4ToU32(new Vector4(176f/255f,100f/255f,0f,1))
            );

            try
            {
                // Then, we do the dance through all relevant nodes on _NaviMap
                // I imagine this throws sometimes because of racing conditions -> We try to access an already freed texture e.g.
                // So we just ignore those small exceptions, it works a few frames later anyways
                var mapScale = miniMapIconsRootComponentNode->Component->ULDData.NodeList[1]->ScaleX;
                var playerPos = new Vector2(playerX, playerY);
                for (var i = 4; i < miniMapIconsRootComponentNode->Component->ULDData.NodeListCount; i++)
                {
                    var mapIconComponentNode =
                        (AtkComponentNode*) miniMapIconsRootComponentNode->Component->ULDData.NodeList[i];
                    if(!mapIconComponentNode->AtkResNode.IsVisible)  continue;
                    for (var j = 2; j < mapIconComponentNode->Component->ULDData.NodeListCount; j++)
                    {
                        // NOTE (Chiv) Invariant: From 2 onward, only ImageNodes
                        var imgNode = (AtkImageNode*)mapIconComponentNode->Component->ULDData.NodeList[j];
#if DEBUG
                        if (imgNode->AtkResNode.Type != NodeType.Image)
                        {
                            SimpleLog.Error($"{i}{j} was not an ImageNode");
                            continue;
                        }
#endif
                        if (!imgNode->AtkResNode.IsVisible || !imgNode->AtkResNode.ParentNode->IsVisible) continue;
                        var part = imgNode->PartsList->Parts[imgNode->PartId];
                        var type = part.ULDTexture->AtkTexture.TextureType;
                        //NOTE (CHIV) Invariant: It should always be a resource
#if DEBUG                        
                        if (type != TextureType.Resource)
                        {
                            SimpleLog.Error($"{i}{j} was not a Resource Texture");
                            continue;
                        };
#endif
                        var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                        var texFileNamePtr =
                            part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        
                        var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                        switch (texString)
                        {
                            case var _ when texString?.EndsWith("060443.tex", StringComparison.InvariantCultureIgnoreCase) ?? false: //Player Marker
                                backgroundDrawList.AddImage(
                                    new IntPtr(tex->D3D11ShaderResourceView)
                                    ,new Vector2(compassCentre.X , compassCentre.Y+22)
                                    ,new Vector2(compassCentre.X + 32 * scale, compassCentre.Y + 22 + 32 *scale)
                                    ,zeroVec
                                    ,oneVec
                                );
                                break;
                            case var _ when texString?.EndsWith("060495.tex", StringComparison.InvariantCultureIgnoreCase) ?? false: // Small Area Circle
                            case var _ when texString?.EndsWith("060496.tex", StringComparison.InvariantCultureIgnoreCase) ?? false: // Big Area Circle
                                // TODO Distinguish between Circles for quests and circles for Fates (colour?)
                                var (scaleArea, angleArea, distanceArea) = GetScaleFactorAndSignedAngle(
                                    playerPos,
                                    new Vector2(
                                        mapIconComponentNode->AtkResNode.X,
                                        mapIconComponentNode->AtkResNode.Y
                                    ),
                                    playerForward,
                                    mapScale);
                                //TODO Adjust for different scale levels...or not.
                                var radius = mapIconComponentNode->AtkResNode.ScaleX * 
                                             (mapIconComponentNode->AtkResNode.Width - mapIconComponentNode->AtkResNode.OriginX);
#if DEBUG
                                _areaCircle.Add((radius, distanceArea));
#endif
                                // TODO Ring, ring, SignedAngle first arg is FROM !
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                ImGui.SameLine(halfWidthOfCompass + compassUnit * -angleArea);
                                ImGui.Image(
                                    new IntPtr(tex->D3D11ShaderResourceView)
                                    ,new Vector2(part.Width, part.Height) * scale * (scaleArea)
                                    ,zeroVec
                                    ,oneVec
                                    , distanceArea < radius
                                        ? new Vector4(
                                            imgNode->AtkResNode.AddRed * (imgNode->AtkResNode.MultiplyRed/100f)/255f,
                                            imgNode->AtkResNode.AddGreen * (imgNode->AtkResNode.MultiplyGreen/100f)/255f,
                                            imgNode->AtkResNode.AddBlue * (imgNode->AtkResNode.MultiplyBlue/100f)/255f,
                                            1)
                                        : new Vector4(1f,1,1f,1f)
                                );
                                break;
                            case var _ when (texString?.EndsWith("NaviMap.tex", StringComparison.InvariantCultureIgnoreCase) ?? false): //Arrows to quests and fates, glowy thingy
                                var u = (float) part.U / tex->Width;
                                var v = (float) part.V / tex->Height;
                                var u1 = (float) (part.U + part.Width) / tex->Width;
                                var v1 = (float) (part.V + part.Height) / tex->Height;
                                // TODO Ring, ring, SignedAngle first arg is FROM !
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                ImGui.SameLine(halfWidthOfCompass + 
                                               compassUnit * 
                                               -GetSignedAngle(mapIconComponentNode->AtkResNode.Rotation, playerForward));
                                ImGui.Image(
                                    naviMapTextureD3D11ShaderResourceView,
                                    new Vector2(part.Width, part.Height) * scale * scaleFactorForRotationBasedDistance,
                                    new Vector2(u,v),
                                    new Vector2(u1,v1)
                                );
                                break;
                            case var _ when texString?.EndsWith("060457.tex") ?? false: // Area Transition Bullet Thingy
                            default:
                                var (scaleFactor, angle, _) = GetScaleFactorAndSignedAngle(
                                    playerPos,
                                    new Vector2(
                                        mapIconComponentNode->AtkResNode.X,
                                        mapIconComponentNode->AtkResNode.Y
                                    ),
                                    playerForward,
                                    mapScale);
                                // TODO Ring, ring, SignedAngle first arg is FROM !
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                ImGui.SameLine(halfWidthOfCompass + compassUnit * -angle);
                                ImGui.Image(
                                    new IntPtr(tex->D3D11ShaderResourceView),
                                    new Vector2(part.Width, part.Height) * scale * scaleFactor,
                                    zeroVec,
                                    oneVec
                                );
                                break;
                                
                        }

                    }
                }

                //TODO (Chiv) Later, we might do that for AreaMap too
            }
            catch
            {
                // ignored
            }

        }

        
        private (float scaleFactor, float signedAngle, float distance) GetScaleFactorAndSignedAngle(in Vector2 from, in Vector2 to, in Vector2 forward, float distanceScaling)
        {
            const float lowestScaleFactor = 0.3f;
            var distanceOffset = 40f * distanceScaling;//80f;
            var maxDistance = 180f * distanceScaling; //;360f;
            //TODO (Chiv) Oh boy, check the math
            var distance = Vector2.Distance(to, from);
            var scaleFactor = Math.Max(1f - (distance-distanceOffset) / maxDistance, lowestScaleFactor);
            //return (scaleFactor,SignedAngle(to  - from, forward), distance);
            return (scaleFactor,SignedAngle(from - to, forward), distance);
        }

        private float GetSignedAngle(in float rotation, in Vector2 forward)
        {
            var cosObject = (float) Math.Cos(rotation);
            var sinObject = (float) Math.Sin(rotation);
            // TODO Wrong math!
            var objectForward = new Vector2(-sinObject, cosObject);
            return SignedAngle(objectForward, forward);
        }

        private bool ShouldHideCompass()
        {
            //TODO ShouldHideCOmpass
            return false;
        }

        //TODO (Chiv) While this is nice and somehow works, it still explodes 1-30 seconds later, so I give up on that for now
        private void OnFrameworkUpdateUpdateAddonCompass(Framework framework)
        {
            // TODO (Chiv) Why use this and not the rotation on the minimap rotation thingy?
            // Minimap rotation thingy is even already flipped!
            // And apparently even accessible & updated if _NaviMap is disabled
            var cameraRotationInRadian = -*(float*) (_maybeCameraStruct + 0x130);
            
            var navimapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (navimapPtr == IntPtr.Zero) return;
            var naviMap = (AtkUnitBase*) navimapPtr;
            //NOTE (chiv) 3 means fully loaded
            if (naviMap->ULDData.LoadedState != 3) return;
            if (!naviMap->IsVisible) return;

            try
            {
                // NOTE (Chiv) This is the position of the player in the minimap coordinate system
                const int playerX = 72, playerY = 72;
                const float distanceOffset = 80f;
                const float maxDistance = 350f;
                const float lowestScaleFactor = 0.3f;
                var playerPos = new Vector2(playerX, playerY);
                var scale = 1f / naviMap->Scale * _config.AddonCompassScale;
                var scaleFactorForRotationBasedDistance = Math.Max(1f - distanceOffset / maxDistance, lowestScaleFactor);
                // TODO (Chiv) My math must be bogus somewhere, cause I need to do same things differently then math would say
                // I think my and the games coordinate system do not agree
                // TODO (Chiv) Redo the math for not locked _NaviMap (might be easier? Should be the same?)
                var playerCos = (float) Math.Cos(cameraRotationInRadian);
                var playerSin = (float) Math.Sin(cameraRotationInRadian);
                //TODO (Chiv) Uhm, actually with H=1, it should be new Vector(cos,sin); that breaks calculations though...
                // Is my Coordinate System the same as the games' minimap?
                var playerViewVector = new Vector2(-playerSin, playerCos);
                // TODO (Chiv) do it all in Radians
                var compassUnit = _config.AddonCompassWidth / 360f;
                //First, the background
                UiHelper.SetSize(_background, _config.AddonCompassWidth, 50);
                _background->PartId = (ushort) _config.AddonCompassBackgroundPartId;
                UiHelper.SetPosition(
                    _background, 
                    _config.AddonCompassOffset.X-_config.AddonCompassWidth/2f,
                    _config.AddonCompassOffset.Y);
                _background->AtkResNode.ScaleX = _background->AtkResNode.ScaleY = scale;
                UiHelper.SetVisible((AtkResNode*) _background, !_config.AddonCompassDisableBackground);
                // Second, we position our Cardinals
                //TODO (Chiv) Uhm, no, east is the other way. Again, coordinate system mismatch?
                var east = -Vector2.UnitX;
                // TODO (Chiv) Yeah, the minus  here is bogus as hell too.
                var south = -Vector2.UnitY;
                var west = Vector2.UnitX;
                var north = Vector2.UnitY;
                // TODO (chiv) actually, SignedAngle first arg is FROM, not TO
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[0],
                    _config.AddonCompassOffset.X + compassUnit * -SignedAngle(east, playerViewVector),
                    _config.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[1],
                    _config.AddonCompassOffset.X + compassUnit * -SignedAngle(south, playerViewVector),
                    _config.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[2],
                    _config.AddonCompassOffset.X + compassUnit * -SignedAngle(west, playerViewVector),
                    _config.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[3],
                    _config.AddonCompassOffset.X + compassUnit * -SignedAngle(north, playerViewVector),
                    _config.AddonCompassOffset.Y);
                
                _cardinalsClonedImageNodes[0]->AtkResNode.ScaleX = _cardinalsClonedImageNodes[0]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[1]->AtkResNode.ScaleX = _cardinalsClonedImageNodes[1]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[2]->AtkResNode.ScaleX = _cardinalsClonedImageNodes[2]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[3]->AtkResNode.ScaleX = _cardinalsClonedImageNodes[3]->AtkResNode.ScaleY = scale;

                //Then, we do the dance through all relevant nodes on _NaviMap
                var iconsRootComponentNode = (AtkComponentNode*) naviMap->ULDData.NodeList[2];
                for (var i = 4; i < iconsRootComponentNode->Component->ULDData.NodeListCount; i++)
                {
                    var iconComponentNode =
                        (AtkComponentNode*) iconsRootComponentNode->Component->ULDData.NodeList[i];
                    for (var j = 2; j < iconComponentNode->Component->ULDData.NodeListCount; j++)
                    {
                        // NOTE (Chiv) Invariant: From 2 onward, only ImageNodes
                        var clone = _clonedImageNodes[i - 4, j - 2];
                        var imgNode = (AtkImageNode*)iconComponentNode->Component->ULDData.NodeList[j];
                        if (imgNode->AtkResNode.Type != NodeType.Image)
                        {
                            SimpleLog.Error($"{i}{j} was not an ImageNode");
                            continue;
                        }
                        clone->PartId = 0;
                        
                        var show = iconComponentNode->AtkResNode.IsVisible && imgNode->AtkResNode.IsVisible;
                        if(!show)
                        {
                            // NOTE (Chiv) Shown + PartsList null explodes
                            UiHelper.Hide(clone);
                            clone->PartsList = null;
                            continue;
                        };
                        clone->PartsList = imgNode->PartsList;
                        clone->PartId = imgNode->PartId;
                        clone->WrapMode = imgNode->WrapMode;
                        clone->AtkResNode.Width = imgNode->AtkResNode.Width;
                        clone->AtkResNode.Height = imgNode->AtkResNode.Height;
                        clone->AtkResNode.AddBlue = imgNode->AtkResNode.AddBlue;
                        clone->AtkResNode.AddGreen = imgNode->AtkResNode.AddGreen;
                        clone->AtkResNode.AddRed = imgNode->AtkResNode.AddRed;
                        clone->AtkResNode.MultiplyBlue = imgNode->AtkResNode.MultiplyBlue;
                        clone->AtkResNode.MultiplyGreen = imgNode->AtkResNode.MultiplyGreen;
                        clone->AtkResNode.MultiplyRed = imgNode->AtkResNode.MultiplyRed;
                        clone->AtkResNode.Color = imgNode->AtkResNode.Color;
                        clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale;
                        UiHelper.Show(clone);
                        var part = imgNode->PartsList->Parts[imgNode->PartId];
                        var type = part.ULDTexture->AtkTexture.TextureType;
                        // OR?? //NOTE (CHIV) It should always be a resource
                        if (type != TextureType.Resource)
                        {
                            SimpleLog.Error($"{i}{j} was not a Resource Texture");
                            continue;
                        };
                        var texFileNamePtr =
                            part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                        switch (texString)
                        {
                            case var _ when texString?.EndsWith("060443.tex", StringComparison.InvariantCultureIgnoreCase) ?? false: //Player Marker
                                UiHelper.SetPosition(
                                    clone,
                                    _config.AddonCompassOffset.X,
                                    _config.AddonCompassOffset.Y);
                                break;
                            case var _ when texString?.EndsWith("060457.tex", StringComparison.InvariantCultureIgnoreCase) ?? false: // Area Transition Bullet Thingy
                                var toObject = new Vector2(playerX - iconComponentNode->AtkResNode.X, playerY - iconComponentNode->AtkResNode.Y);
                                var distanceToObject = Vector2.Distance(
                                    new Vector2(iconComponentNode->AtkResNode.X, iconComponentNode->AtkResNode.Y),
                                    playerPos) - distanceOffset;
                                var scaleFactor = Math.Max(1f - distanceToObject / maxDistance, lowestScaleFactor);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale * scaleFactor;
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                UiHelper.SetPosition(
                                    clone,
                                    _config.AddonCompassOffset.X + compassUnit * -SignedAngle(toObject, playerViewVector),
                                    _config.AddonCompassOffset.Y);
                                break;
                            case var _ when (texString?.EndsWith("NaviMap.tex", StringComparison.InvariantCultureIgnoreCase) ?? false) && imgNode->PartId == 21:
                                UiHelper.Hide(clone);
                                break;
                            case var _ when iconComponentNode->AtkResNode.Rotation == 0:
                                var toObject2 = new Vector2(playerX - iconComponentNode->AtkResNode.X, playerY - iconComponentNode->AtkResNode.Y);
                                var distanceToObject2 = Vector2.Distance(
                                    new Vector2(iconComponentNode->AtkResNode.X, iconComponentNode->AtkResNode.Y),
                                    playerPos) - distanceOffset;
                                var scaleFactor2 = Math.Max(1f - distanceToObject2 / maxDistance, lowestScaleFactor);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale * scaleFactor2;
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                UiHelper.SetPosition(
                                    clone,
                                    _config.AddonCompassOffset.X + compassUnit * -SignedAngle(toObject2, playerViewVector),
                                    _config.AddonCompassOffset.Y);
                                break;
                            default:
                                var cosArrow = (float) Math.Cos(iconComponentNode->AtkResNode.Rotation);
                                var sinArrow = (float) Math.Sin(iconComponentNode->AtkResNode.Rotation);
                                // TODO (Chiv) Wrong again!
                                var toObject3 = new Vector2(-sinArrow, cosArrow);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale * scaleFactorForRotationBasedDistance;
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                UiHelper.SetPosition(
                                    clone,
                                    _config.AddonCompassOffset.X + compassUnit * -SignedAngle(toObject3, playerViewVector),
                                    _config.AddonCompassOffset.Y);
                                break;
                                
                        }

                    }
                }

                //TODO (Chiv) Later, we might do that for AreaMap too
            }
            catch
            {
                // ignored
            }


            
        }

        //TODO (Chiv) See comment on OnFrameworkUpdateUpdateAddonCompass
        private void OnFrameworkUpdateSetupAddonNodes(Framework framework)
        {
            try
            {
                var navimapPtr = _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
                if (navimapPtr == IntPtr.Zero) return;
                var naviMap = (AtkUnitBase*) navimapPtr;
                //NOTE (chiv) 3 means fully loaded
                if (naviMap->ULDData.LoadedState != 3) return;
                //NOTE (chiv) There should be no real need to care for visibility for cloning but oh well, untested.
                if (!naviMap->IsVisible) return;
                //NOTE (chiv) 19 should be the unmodified default
                //if (naviMap->ULDData.NodeListCount != 19) return;
                
                UiHelper.ExpandNodeList(naviMap, (ushort)_clonedImageNodes.Length);
                var prototypeImgNode = naviMap->ULDData.NodeList[11];
                var currentNodeId = uint.MaxValue;

                //First, Background (Reusing empty slot)
                _background = (AtkImageNode*)CloneAndAttach(
                    ref naviMap->ULDData,
                    naviMap->RootNode,
                    prototypeImgNode,
                    currentNodeId--);
                _background->WrapMode = 2;
                if(_config.AddonCompassDisableBackground) UiHelper.Hide(_background);

                // Next, Cardinals
                for (var i = 0; i < _cardinalsClonedImageNodes.Length; i++)
                {
                    _cardinalsClonedImageNodes[i] = (AtkImageNode*)CloneAndAttach(
                        ref naviMap->ULDData,
                        naviMap->RootNode,
                        naviMap->ULDData.NodeList[i+9],
                        currentNodeId--);

                }

                // NOTE (Chiv) Set for rest
                // This is overkill but we are basically mimicking the amount of nodes in _NaviMap.
                // It could be that all are used!
                for (var i = 0; i < _clonedImageNodes.GetLength(0); i++)
                {
                    for (var j = 0; j < _clonedImageNodes.GetLength(1); j++)
                    {
                        _clonedImageNodes[i,j] = (AtkImageNode*)CloneAndAttach(
                            ref naviMap->ULDData,
                            naviMap->RootNode,
                            prototypeImgNode,
                            currentNodeId--);
                        // Explodes if PartsList = null before Hiding it
                        UiHelper.Hide(_clonedImageNodes[i,j]);
                        _clonedImageNodes[i,j]->PartId = 0;
                        _clonedImageNodes[i,j]->PartsList = null;

                    }
                }
                
                //TODO (Chiv) Search for PlayerIcon and set it up too?

                _pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdateSetupAddonNodes;
                _pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdateUpdateAddonCompass;
            }
            catch(Exception e)
            {
                SimpleLog.Error(e);
            }
        }

        private AtkResNode* CloneAndAttach(ref ULDData uld, AtkResNode* parent, AtkResNode* prototype, uint nodeId = uint.MaxValue)
        {
            var clone = UiHelper.CloneNode(prototype);
            clone->NodeID = nodeId;
            clone->ParentNode = parent;
            var currentLastSibling = parent->ChildNode;
            while (currentLastSibling->PrevSiblingNode != null)
            {
                currentLastSibling = currentLastSibling->PrevSiblingNode;
            }
            clone->PrevSiblingNode = null;
            clone->NextSiblingNode = currentLastSibling;
            currentLastSibling->PrevSiblingNode = clone;
            uld.NodeList[uld.NodeListCount++] = clone;
            return clone;
        }
        private unsafe void OnFrameworkUpdate(Framework framework)
        {
            
            if (_imageNodes.Count > 0)
            {
                var f = (AtkImageNode*)_imageNodes[0];
                ((AtkImageNode*) f)->PartId = (ushort)_partId;
                // NOTE WrapMode 1 -> ignore UV and take Width/Size != size, 2-> Fit/Stretch, 0 -> ignore Width/Height, do UV only
                // Seems like game uses 1 + correct width/size, matching UV 
                ((AtkImageNode*) f)->WrapMode = (byte)_wrapMode;
                ((AtkImageNode*) f)->AtkResNode.Rotation = _rotation;
                UiHelper.SetSize(f, _width, _height);

                var minimap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
                if (new IntPtr(minimap) == IntPtr.Zero) return;

                if (minimap->ULDData.LoadedState != 3)
                {
                    return;
                }
                var rotationInRadian = -*(float*) (_maybeCameraStruct + 0x130);
                var playerX = 72;
                var playerY = 72;
                var cos = (float) Math.Cos(rotationInRadian);
                var sin = (float) Math.Sin(rotationInRadian);
                //TODO (Chiv) Uhm, actually with H=1, it should be new Vector(cos,sin); that breaks calculations though...
                // Is my Coordinate System the same as the games' minimap?
                var playerViewVector2 = new Vector2(-sin, cos);
                var widthOfCompass = 450;
                var compassUnit = widthOfCompass / 360f;
                var x = -1;
                var y = 0;
                var toObject = new Vector2(x, y);
                var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                f->AtkResNode.ScaleX = 1f / minimap->Scale * _scale;
                f->AtkResNode.ScaleY = 1f / minimap->Scale * _scale;
                UiHelper.SetPosition(f, _compassOffset.X + compassPos.X, _compassOffset.Y);
                
                //_atkResNodeSetPositionFloat(new IntPtr(f), _compassOffset.X + compassPos.X, _compassOffset.Y);

            }
            if (_destroy)
            {
                _destroy = false;
                foreach (var nodePtr in _imageNodes)
                {
                    _destroyAtkImageNode(nodePtr, true);
                }
            }
            if (_recalculate)
            {
                _recalculate = false;
                foreach (var nodePtr in _imageNodes)
                {
                    var imageNode = (AtkImageNode*) nodePtr;
                    var x = imageNode->AtkResNode.X;
                    var y = imageNode->AtkResNode.Y;
                    if (_reset)
                    {
                        _atkResNodeSetPositionFloat(
                            nodePtr,
                            0,
                            0);
                        _reset = false;
                    }
                    else
                    {
                        _atkResNodeSetPositionFloat(
                            nodePtr,
                            _compassOffset.X + x,
                            _compassOffset.Y + y);
                    }

                }
            }
            
            if (!_refresh) return;
            _refresh = false;
            PluginLog.Log("Refreshing Compass");
            var naviMap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (new IntPtr(naviMap) == IntPtr.Zero) return;

            if (naviMap->ULDData.LoadedState != 3)
            {
                return;
            }
            var cameraRotationInRadian = -*(float*) (_maybeCameraStruct + 0x130);
            if (_replaceTexture)
            {
                _replaceTexture = false;
                var baseComponentNode = (AtkComponentNode*)naviMap->ULDData.NodeList[2];
                var imageBaseComponentNode = (AtkComponentNode*)baseComponentNode->Component->ULDData.NodeList[7];
                var imageNode = (AtkImageNode*)imageBaseComponentNode->Component->ULDData.NodeList[3];
                if (imageNode->AtkResNode.Type == NodeType.Image)
                {
                    if (_imageNodes.Count > 0)
                    {
                        var f = (AtkImageNode*)_imageNodes[0];
                        f->PartId = imageNode->PartId;
                        f->WrapMode = imageNode->WrapMode;
                        f->PartsList = imageNode->PartsList;
                        f->AtkResNode.Width = imageNode->AtkResNode.Width;
                        f->AtkResNode.Height = imageNode->AtkResNode.Height;
                        _partId = f->PartId;
                    }
                    else
                    {
                        SimpleLog.Log("Nothing in _imageNodes");
                    }
                }
                else
                {
                    SimpleLog.Log("Was not an ImageNode");
                }
            }
            else
            {
                for (int i = 9; i < 10 /*13*/; i++)
                {
                    var imgNode = (AtkImageNode*) naviMap->ULDData.NodeList[i];
                    var clone = UiHelper.CloneNode(imgNode);
                    _imageNodes.Add(new IntPtr(clone));

                    UiHelper.ExpandNodeList(naviMap, 1);
                    clone->AtkResNode.ParentNode = naviMap->ULDData.NodeList[0];
                    clone->AtkResNode.PrevSiblingNode = null;
                    clone->AtkResNode.NextSiblingNode = naviMap->ULDData.NodeList[18];
                    naviMap->ULDData.NodeList[18]->PrevSiblingNode = (AtkResNode*) clone;
                    naviMap->ULDData.NodeList[naviMap->ULDData.NodeListCount++] = (AtkResNode*) clone;
                    _partId = clone->PartId;
                    _wrapMode = clone->WrapMode;
                    _width = clone->AtkResNode.Width;
                    _height = clone->AtkResNode.Height;
                    _rotation = clone->AtkResNode.Rotation;
                    continue;
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
                    //TODO (Chiv) Uhm, actually with H=1, it should be new Vector(cos,sin); that breaks calculations though...
                    // Is my Coordinate System the same as the games' minimap?
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
            }

            return;
            if (_clonedUnitBase is null)
            {
                var t = framework.Gui.GetUiObjectByName("NamePlate", 1);
                if (t == IntPtr.Zero) return;
                var exp = (AtkUnitBase*) t;
                if (exp->ULDData.LoadedState != 3) return;
                if (!exp->IsVisible) return;
                _clonedUnitBase = UiHelper.CloneNode(exp, false, false, false);
                _clonedUnitBase->Name[0] = 71;
                //_clonedUnitBase->RootNode->Width = 1920;
                //_clonedUnitBase->RootNode->Height = 1920;
                //_atkUnitBaseSetPosition(new IntPtr(_clonedUnitBase), 0, 0);
                
                var stage = _UIDebug.GetAtkStageSingletonFunc();
                var layerOneList = &stage->RaptureAtkUnitManager->AtkUnitManager.DepthLayerTwoList;
                (&layerOneList->AtkUnitEntries)[layerOneList->Count++] = _clonedUnitBase;
                // TODO Add to LoadedUnits and Unit16 is good or bad? -> Crashes now ¯\_(ツ)_/¯
                //var allLoaded = &stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
                //(&allLoaded->AtkUnitEntries)[layerOneList->Count++] = _clonedUnitBase;
                //var unitList16 = &stage->RaptureAtkUnitManager->AtkUnitManager.UnitList16;
                //(&unitList16->AtkUnitEntries)[layerOneList->Count++] = _clonedUnitBase;
                //UiHelper.SetPosition(_clonedUnitBase, 80,0);
                //UiHelper.SetPosition(_clonedUnitBase->RootNode, 80,0);
                //var txtNode = (AtkTextNode*)UiHelper.CloneNode((AtkTextNode*) exp->ULDData.NodeList[3]);
                
                var txtNode = (AtkTextNode*)_createAtkNode(IntPtr.Zero, NodeType.Text);
                txtNode->AtkResNode.Type = NodeType.Text;
                txtNode->AlignmentFontType = 3;
                txtNode->FontSize = 12;
                txtNode->AtkResNode.Width = 256;
                txtNode->AtkResNode.Height = 22;
                txtNode->TextFlags = 24;
                txtNode->AtkResNode.NodeID = 2;
                txtNode->LineSpacing = 12;
                txtNode->AtkResNode.ScaleX = 1;
                txtNode->AtkResNode.ScaleY = 1;
                txtNode->AtkResNode.Flags = 8243;
                txtNode->AtkResNode.Flags_2 = 9;
                txtNode->SelectStart = 0;
                txtNode->SelectEnd = 0;
                //txtNode->AtkResNode.Y = 150;
                //txtNode->AtkResNode.X = 150;
                txtNode->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)txtNode->NodeText.BufSize);
                UiHelper.SetText(txtNode, "I AM A ALLOCATED THINGY? RUN!");

                _clonedTxtNode = txtNode;
                //UiHelper.SetPosition(txtNode, 100f, -250f);  s
                //UiHelper.SetPosition(_clonedUnitBase->ULDData.NodeList[0], 0f,0f);
                txtNode->AtkResNode.ParentNode = _clonedUnitBase->ULDData.NodeList[0];
                _clonedUnitBase->ULDData.NodeList[0]->ChildNode = (AtkResNode*)txtNode;
                _clonedUnitBase->ULDData.NodeList[0]->ChildCount++;
                //txtNode->AtkResNode.NextSiblingNode = _clonedUnitBase->ULDData.NodeList[5];
                //_clonedUnitBase->ULDData.NodeList[5]->PrevSiblingNode = (AtkResNode*)_clonedTxtNode;
                UiHelper.ExpandNodeList(_clonedUnitBase, 1);
                
                _clonedUnitBase->ULDData.NodeList[_clonedUnitBase->ULDData.NodeListCount++] = (AtkResNode*)txtNode;
                
                //UiHelper.SetPosition(_clonedUnitBase->RootNode->ChildNode, 0,30);
                //_atkUnitBaseSetPosition(new IntPtr(_clonedUnitBase), 80, 0);
                //_atkResNodeSetPositionFloat(new IntPtr(_clonedUnitBase->RootNode), 80 ,0);
                //_atkResNodeSetPositionFloat(new IntPtr(txtNode), 0, 20);
            }

            return;
            if (_clonedTxtNode is null)
            {
                SimpleLog.Log($"Cloning EXP node");
                
                var exp = (AtkUnitBase*)framework.Gui.GetUiObjectByName("_Exp", 1);
                UiHelper.ExpandNodeList(exp, 1);
                
                var text = (AtkTextNode*) exp->ULDData.NodeList[3];
                _clonedTxtNode = UiHelper.CloneNode(text);
                
                _clonedTxtNode->NodeText.StringPtr = (byte*)UiHelper.Alloc((ulong)_clonedTxtNode->NodeText.BufSize);
                UiHelper.SetText(_clonedTxtNode, "Skill");
                
                //_cloneBase->AtkResNode.X = NewWidth - (AddedWidth + 60);
                //_cloneBase->AtkResNode.Width = AddedWidth;
                //_cloneBase.Po
                //UiHelper.SetPosition(_cloneBase);
                _clonedTxtNode->AtkResNode.ParentNode = exp->ULDData.NodeList[0];
                _clonedTxtNode->AtkResNode.NextSiblingNode = exp->ULDData.NodeList[5];
                exp->ULDData.NodeList[5]->PrevSiblingNode = (AtkResNode*)_clonedTxtNode;
                exp->ULDData.NodeList[exp->ULDData.NodeListCount++] = (AtkResNode*)_clonedTxtNode;
            }

            
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
            ImGui.SetNextWindowSizeConstraints(new Vector2(250f,60f *ImGui.GetIO().FontGlobalScale), new Vector2(int.MaxValue,60f*ImGui.GetIO().FontGlobalScale));
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
                    //TODO (Chiv) Uhm, actually with H=1, it should be new Vector(cos,sin); that breaks calculations though...
                    // Is my Coordinate System the same as the games' minimap?
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
                          
                        ) 
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
                            (texString!.EndsWith("060457.tex")) // Area Transition Bullet Thingy
                            
                        ) 
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
                            // THAT IS TRUE IN THIS CASE GOSH! Area thingy uses X/Y NOT Rotation
                            var toObject = baseComponentNode->AtkResNode.Rotation != 0
                                ? new Vector2(playerX - x, playerY - y)
                                : new Vector2(-sinArrow, cosArrow);

                            var maxDistance = 350f;
                            var distanceToObject = baseComponentNode->AtkResNode.Rotation != 0
                                ? Vector2.Distance(new Vector2(x, y), new Vector2(playerX, playerY)) - 80
                                : 0;
                            var scaleFactor = Math.Max(1f - distanceToObject / maxDistance, 0.5f);
                            //ImGui.Text($"{distanceToObject}");
                            var signedAngle = ((float) Math.Atan2(toObject.Y, toObject.X) -
                                               (float) Math.Atan2(playerViewVector.Y, playerViewVector.X));
                            var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                            //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                            var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                            //ImGui.Text(""+baseComponentNode->AtkResNode.Rotation);
                            ImGui.SameLine(widthOfCompass / 2f + compassPos.X);
                            ImGui.Image(
                                new IntPtr(tex->D3D11ShaderResourceView),
                                imgNode->PartId == 0
                                    ? new Vector2(Math.Min(tex->Width, 40), Math.Min(tex->Height, 40)) * ImGui.GetIO().FontGlobalScale * scaleFactor
                                    : new Vector2(Math.Min(part.Width,(short) 40), Math.Min(part.Height,(short) 40)) * ImGui.GetIO().FontGlobalScale * scaleFactor
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
                        else if (
                            (texString!.EndsWith("060496.tex")) // Big QuestArea Circle 
                          
                        ) 
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

                            var maxDistance = 350f;
                            var distanceToObject = baseComponentNode->AtkResNode.Rotation == 0
                                ? Vector2.Distance(new Vector2(x, y), new Vector2(playerX, playerY)) - 80
                                : 0;
                            var radius = baseComponentNode->AtkResNode.ScaleX * (baseComponentNode->AtkResNode.Width -
                                baseComponentNode->AtkResNode.OriginX);
                            var scaleFactor = Math.Max(1f - distanceToObject / maxDistance, 0.5f);
                            //ImGui.Text($"{distanceToObject}");
                            var signedAngle = ((float) Math.Atan2(toObject.Y, toObject.X) -
                                               (float) Math.Atan2(playerViewVector.Y, playerViewVector.X));
                            var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                            //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                            var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                            //ImGui.Text(""+baseComponentNode->AtkResNode.Rotation);
                            //ImGui.Text($"{imgNode->AtkResNode.AddRed} {imgNode->AtkResNode.AddGreen} {imgNode->AtkResNode.AddBlue}");
                            //ImGui.Text($"{imgNode->AtkResNode.AddRed_2} {imgNode->AtkResNode.AddGreen_2} {imgNode->AtkResNode.AddBlue_2}");
                            ImGui.SameLine(widthOfCompass / 2f + compassPos.X);
                            ImGui.Image(
                                new IntPtr(tex->D3D11ShaderResourceView),
                                imgNode->PartId == 0
                                    ? new Vector2(Math.Min(tex->Width, 40), Math.Min(tex->Height, 40)) * ImGui.GetIO().FontGlobalScale * scaleFactor
                                    : new Vector2(Math.Min(part.Width,(short) 40), Math.Min(part.Height,(short) 40)) * ImGui.GetIO().FontGlobalScale * scaleFactor
                                , imgNode->PartId == 0 ? new Vector2(0, 0) : new Vector2(u, v)
                                , imgNode->PartId == 0 ? new Vector2(1, 1) : new Vector2(u1, v1)
                                , distanceToObject < radius
                                ? new Vector4(
                                    (imgNode->AtkResNode.AddRed + imgNode->AtkResNode.MultiplyRed)/255f,
                                    (imgNode->AtkResNode.AddGreen+ imgNode->AtkResNode.MultiplyGreen)/255f,
                                    (imgNode->AtkResNode.AddBlue+ imgNode->AtkResNode.MultiplyBlue)/255f,
                                    1)
                                : new Vector4(1f,1,1f,1f)

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
                        /*
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
                        */
                        // NOTE (Chiv): This is hoping that no other texture besides navimap.tex ever uses PartId 21
                        // TODO Bongers, we need to read the texString, there is no away around that for conditionals and special handling
                        else if(imgNode->PartId != 21)//if (!texString?.EndsWith("navimap.tex") ?? true)
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

                            var maxDistance = 360f;
                            var distanceToObject = baseComponentNode->AtkResNode.Rotation == 0
                                ? Vector2.Distance(new Vector2(x, y), new Vector2(playerX, playerY)) - 80
                                : 80;
                            var scaleFactor = Math.Max(1f - distanceToObject / maxDistance, 0.5f);
                            //ImGui.Text($"{distanceToObject}");
                            var signedAngle = ((float) Math.Atan2(toObject.Y, toObject.X) -
                                               (float) Math.Atan2(playerViewVector.Y, playerViewVector.X));
                            var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                            //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                            var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                            ImGui.SameLine(widthOfCompass / 2f + compassPos.X);
                            ImGui.Image(
                                new IntPtr(tex->D3D11ShaderResourceView),
                                imgNode->PartId == 0
                                    ? new Vector2(Math.Min(tex->Width, 40), Math.Min(tex->Height, 40)) * ImGui.GetIO().FontGlobalScale * scaleFactor
                                    : new Vector2(Math.Min(part.Width,(short) 40), Math.Min(part.Height,(short) 40)) * ImGui.GetIO().FontGlobalScale * scaleFactor
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
            BuildImGuiCompass();
            if (!_buildingConfigUi) return;
            var (shouldBuildConfigUi, changedConfig) = ConfigurationUi.DrawConfigUi(_config);
            if (changedConfig)
            {
                _dirty = true;
                _pluginInterface.SavePluginConfig(_config);
            }
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
                _naviMap?.Dispose();
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