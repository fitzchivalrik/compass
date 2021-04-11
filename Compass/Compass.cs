using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Data.LuminaExtensions;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
using FFXIVClientStructs.Component.GUI.ULD;
using ImGuiNET;
using ImGuiScene;
using Lumina.Data.Files;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.Helper;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;
using static Compass.Extensions;

// TODO 5 Refactor DrawCombo to generic
namespace Compass
{
    public unsafe class Compass : IDisposable
    {
        private const string Command = "/compass";
        public const string PluginName = "Compass";
        private readonly Hook<ClampToMinusPiAndPiDelegate> _clampToMinusPiAndPi;


        private readonly Hook<AtkResNode_SetPositionFloat> _resNodePositionFloatHook;
        private readonly Hook<AtkResNode_SetPositionShort> _resNodePositionShortHook;
        private readonly Hook<SetCameraRotationDelegate> _setCameraRotation;
        private readonly Stopwatch _stopwatch = new();
        private readonly Hook<AtkUnitBase_SetPosition> _unitBaseSetPositionHook;
        private AtkResNode_SetPositionFloat _atkResNodeSetPositionFloat;
        private AtkResNode_SetPositionShort _atkResNodeSetPositionShort;
        private AtkUnitBase_SetPosition _atkUnitBaseSetPosition;
        private AtkImageNode* _background;
        private bool _buildingConfigUi;
        private float _calledWithDegree;
        private float _calledWithDegreeFromSetCameraRotation;
        private nint _cameraBase;
        private nint _cameraManager;

        private readonly AtkImageNode*[] _cardinalsClonedImageNodes = new AtkImageNode*[4];

        //NOTE (Chiv) 202 Component Nodes for the Icons on the minimap + 1*4 pointers for the Cardinals
        // Actually, there are 101*3 and 101*4 Component nodes but this it easier to deal with +
        // one can abuse the first 101,4 for something else
        // like [1,3] == backgroundNode
        private readonly AtkImageNode*[,] _clonedImageNodes = new AtkImageNode*[202, 4];
        private AtkTextNode* _clonedTxtNode;
        private AtkUnitBase* _clonedUnitBase;
        private readonly TextureWrap _compassImage;
        private Vector2 _compassOffset = new(-360, 150);
        private CreateAtkNode _createAtkNode;
        private AtkImageNode_Destroy _destroyAtkImageNode;
        private bool _dirty;
        private nint _gameCameraObject;

        private List<nint> _imageNodes = new(150);
        private bool _isDisposed;
        private long _logTicks = Stopwatch.Frequency * 2;
        private nint _maybeCameraStruct;
        private readonly TextureWrap _naviMap;
        private bool _reset;
        private float _scale = 1;
        private nint _sceneCameraObject;
        private float _setToDegree;
        private bool _shouldUpdate;
        private nint _unknown;

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


            PluginInterface = pi;
            PluginConfig = config;

            PluginInterface.ClientState.OnLogin += OnLogin;
            PluginInterface.ClientState.OnLogout += OnLogout;

            #region Hooks, Functions and Addresses

            _clampToMinusPiAndPi = new Hook<ClampToMinusPiAndPiDelegate>(
                PluginInterface.TargetModuleScanner.ScanText(clampToMinusPiAndPiSignature),
                (ClampToMinusPiAndPiDelegate) ClampToMinusPiAndPi);
            _clampToMinusPiAndPi.Enable();

            _setCameraRotation = new Hook<SetCameraRotationDelegate>(
                PluginInterface.TargetModuleScanner.ScanText(setCameraRotationSignature),
                (SetCameraRotationDelegate) SetCameraRotation);
            _setCameraRotation.Enable();

            _sceneCameraObject = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig(sceneCameraCtorSig, 0xC);
            _gameCameraObject = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig(gameCameraCtorSig);
            _cameraBase = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig(cameraBaseSig);
            _cameraManager = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig(cameraManagerSignature);


            _atkUnitBaseSetPosition = Marshal.GetDelegateForFunctionPointer<AtkUnitBase_SetPosition>(
                PluginInterface.TargetModuleScanner.ScanText(atkUnitBaseSetPositionSignature));
            _atkResNodeSetPositionShort = Marshal.GetDelegateForFunctionPointer<AtkResNode_SetPositionShort>(
                PluginInterface.TargetModuleScanner.ScanText(atkResNodeSetPositionShortSignature));
            _atkResNodeSetPositionFloat = Marshal.GetDelegateForFunctionPointer<AtkResNode_SetPositionFloat>(
                PluginInterface.TargetModuleScanner.ScanText(atkResNodeSetPositionFloatSignature));
            _createAtkNode = Marshal.GetDelegateForFunctionPointer<CreateAtkNode>(
                PluginInterface.TargetModuleScanner.ScanText(createAtkNodeSignature));
            _destroyAtkImageNode = Marshal.GetDelegateForFunctionPointer<AtkImageNode_Destroy>(
                PluginInterface.TargetModuleScanner.ScanText(atkImageNodeDestroySignature));

            #endregion

            #region Excel Data

            #endregion
            
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

            if (PluginInterface.Reason == PluginLoadReason.Installer
                   || PluginInterface.ClientState.LocalPlayer is not null
            )
                OnLogin(null!, null!);
#endif
        }

        public Configuration PluginConfig { get; }

        public DalamudPluginInterface PluginInterface { get; }


        private void SetCameraRotation(nint cameraThis, float degree)
        {
            _shouldUpdate = true;
            _setCameraRotation.Original(cameraThis, degree);
            _shouldUpdate = false;
            _maybeCameraStruct = cameraThis;
            _calledWithDegreeFromSetCameraRotation = degree;
#if RELEASE
           _setCameraRotation.Disable(); 
#endif
        }

        private float ClampToMinusPiAndPi(float degree)
        {
            var original = _clampToMinusPiAndPi.Original(degree);
            if (!_shouldUpdate) return original;
            _calledWithDegree = degree;
            _setToDegree = original;

            return original;
        }


        private void OnLogout(object sender, EventArgs e)
        {
            PluginInterface.UiBuilder.OnOpenConfigUi -= OnOpenConfigUi;
            PluginInterface.UiBuilder.OnBuildUi -= BuildUi;
            //_pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdate;
            //_pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdateSetupAddonNodes;
            //_pluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdateUpdateAddonCompass;
        }

        private void OnLogin(object sender, EventArgs e)
        {
            PluginInterface.UiBuilder.OnOpenConfigUi += OnOpenConfigUi;
            PluginInterface.UiBuilder.OnBuildUi += BuildUi;
            //_pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
            //_pluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdateSetupAddonNodes;
        }

        private void BuildImGuiCompass()
        {
            // TODO (Chiv) Why use this and not the rotation on the minimap rotation thingy?
            // Minimap rotation thingy is even already flipped!
            // And apparently even accessible & updated if _NaviMap is disabled
            if (!PluginConfig.ImGuiCompassEnable) return;
            var naviMapPtr = PluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            if (naviMapPtr == IntPtr.Zero) return;
            var naviMap = (AtkUnitBase*) naviMapPtr;
            //NOTE (chiv) 3 means fully loaded
            if (naviMap->ULDData.LoadedState != 3) return;
            // TODO (chiv) Check the flag if _NaviMap is hidden in the HUD
            if (!naviMap->IsVisible) return;
            if (ShouldHideCompass()) return;
            var scale = PluginConfig.ImGuiCompassScale * ImGui.GetIO().FontGlobalScale;
            const float windowHeight = 50f;
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
                                           | ImGuiWindowFlags.NoMove
                                           | ImGuiWindowFlags.NoMouseInputs
                                           | ImGuiWindowFlags.NoFocusOnAppearing
                                           | ImGuiWindowFlags.NoBackground
                                           | ImGuiWindowFlags.NoNav
                                           | ImGuiWindowFlags.NoInputs
                                           | ImGuiWindowFlags.NoCollapse;
            ImGui.SetNextWindowSizeConstraints(
                new Vector2(250f, (windowHeight + 20) * scale),
                new Vector2(int.MaxValue, (windowHeight + 20) * scale));
            if (!ImGui.Begin("###ImGuiCompassWindow"
                , _buildingConfigUi
                    ? ImGuiWindowFlags.NoCollapse
                      | ImGuiWindowFlags.NoTitleBar
                      | ImGuiWindowFlags.NoFocusOnAppearing
                      | ImGuiWindowFlags.NoScrollbar
                      | ImGuiWindowFlags.NoBackground
                    : flags)
            )
            {
                ImGui.End();
                return;
            }

            // NOTE (Chiv) This is the position of the player in the minimap coordinate system
            const int playerX = 72, playerY = 72;
            const uint whiteColor = 0xFFFFFFFF;
            var cameraRotationInRadian = -*(float*) (_maybeCameraStruct + 0x130);
            var miniMapIconsRootComponentNode = (AtkComponentNode*) naviMap->ULDData.NodeList[2];
            // This leads to jerky behaviour
            //var cameraRotationInRadian = miniMapIconsRootComponentNode->Component->ULDData.NodeList[2]->Rotation;
            //var scaleFactorForRotationBasedDistance = Math.Max(1f - 0 / maxDistance, lowestScaleFactor) * ImGui.GetIO().FontGlobalScale;
            var scaleFactorForRotationBasedDistance = scale;
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
            var halfWidthOfCompass = widthOfCompass * 0.5f;
            var halfHeightOfCompass = windowHeight * 0.5f * scale;
            var compassUnit = widthOfCompass / 360f;
            var westCardinalAtkImageNode = (AtkImageNode*) naviMap->ULDData.NodeList[11];
            // TODO (Chiv) Cache on TerritoryChange/Initialisation?
            var naviMapTextureD3D11ShaderResourceView = new IntPtr(
                westCardinalAtkImageNode->PartsList->Parts[0]
                    .ULDTexture->AtkTexture.Resource->KernelTextureObject->D3D11ShaderResourceView
            );

            var drawList = ImGui.GetWindowDrawList();
            var backgroundDrawList = ImGui.GetBackgroundDrawList();
            var cursorPosition = ImGui.GetCursorScreenPos() - new Vector2(7, 0);
            var compassCentre =
                new Vector2(cursorPosition.X + halfWidthOfCompass, cursorPosition.Y + halfHeightOfCompass);
            var halfWidth32 = 16 * scale;
            var backgroundPMin = new Vector2(compassCentre.X - 5 - halfWidthOfCompass,
                compassCentre.Y - halfHeightOfCompass * 0.5f - 2);
            var backgroundPMax = new Vector2(compassCentre.X + 5 + halfWidthOfCompass,
                compassCentre.Y + halfHeightOfCompass * 0.5f + 2);
            // TODO (Chiv) Draw Background
            //First, the background
            if (PluginConfig.ImGuiCompassEnableBackground)
            {
                if (PluginConfig.ImGuiCompassFillBackground)
                    backgroundDrawList.AddRectFilled(
                        backgroundPMin
                        , backgroundPMax
                        , ImGui.ColorConvertFloat4ToU32(PluginConfig.ImGuiBackgroundColour)
                        , PluginConfig.ImGuiCompassBackgroundRounding
                    );
                if (PluginConfig.ImGuiCompassDrawBorder)
                    backgroundDrawList.AddRect(
                        backgroundPMin - Vector2.One
                        , backgroundPMax + Vector2.One
                        , ImGui.ColorConvertFloat4ToU32(PluginConfig.ImGuiBackgroundBorderColour)
                        , PluginConfig.ImGuiCompassBackgroundRounding
                    );
            }

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
            var halfWidth20 = 10 * scale;
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth20 + eastOffset, compassCentre.Y - halfWidth32) // Width = 20
                , new Vector2(compassCentre.X + eastOffset + halfWidth20, compassCentre.Y + halfWidth32)
                , new Vector2(0.5446429f, 0.8301887f)
                , new Vector2(0.5892857f, 0.9811321f)
            );
            var southOffset = compassUnit * -SignedAngle(south, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth20 + southOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + southOffset + halfWidth20, compassCentre.Y + halfWidth32)
                , new Vector2(0.5892857f, 0.8301887f)
                , new Vector2(0.6339286f, 0.9811321f)
            );
            var westOffset = compassUnit * -SignedAngle(west, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth32 + westOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + westOffset + halfWidth32, compassCentre.Y + halfWidth32)
                , new Vector2(0.4732143f, 0.8301887f)
                , new Vector2(0.5446429f, 0.9811321f)
            );
            var northOffset = compassUnit * -SignedAngle(north, playerForward);
            backgroundDrawList.AddImage(
                naviMapTextureD3D11ShaderResourceView
                , new Vector2(compassCentre.X - halfWidth32 + northOffset, compassCentre.Y - halfWidth32)
                , new Vector2(compassCentre.X + northOffset + halfWidth32, compassCentre.Y + halfWidth32)
                , new Vector2(0.4017857f, 0.8301887f)
                , new Vector2(0.4732143f, 0.9811321f)
                , ImGui.ColorConvertFloat4ToU32(new Vector4(176f / 255f, 100f / 255f, 0f, 1))
            );

            try
            {
                // Then, we do the dance through all relevant nodes on _NaviMap
                // I imagine this throws sometimes because of racing conditions -> We try to access an already freed texture e.g.
                // So we just ignore those small exceptions, it works a few frames later anyways
                var mapScale =
                    miniMapIconsRootComponentNode->Component->ULDData.NodeList[1]->ScaleX; //maxZoom level == 2
                var playerPos = new Vector2(playerX, playerY);
                for (var i = 4; i < miniMapIconsRootComponentNode->Component->ULDData.NodeListCount; i++)
                {
                    var mapIconComponentNode =
                        (AtkComponentNode*) miniMapIconsRootComponentNode->Component->ULDData.NodeList[i];
                    if (!mapIconComponentNode->AtkResNode.IsVisible) continue;
                    for (var j = 2; j < mapIconComponentNode->Component->ULDData.NodeListCount; j++)
                    {
                        // NOTE (Chiv) Invariant: From 2 onward, only ImageNodes
                        var imgNode = (AtkImageNode*) mapIconComponentNode->Component->ULDData.NodeList[j];
#if DEBUG
                        if (imgNode->AtkResNode.Type != NodeType.Image)
                        {
                            SimpleLog.Error($"{i}{j} was not an ImageNode");
                            continue;
                        }
#endif
                        if (!imgNode->AtkResNode.IsVisible || !imgNode->AtkResNode.ParentNode->IsVisible) continue;
                        var part = imgNode->PartsList->Parts[imgNode->PartId];
                        //NOTE (CHIV) Invariant: It should always be a resource
#if DEBUG
                        var type = part.ULDTexture->AtkTexture.TextureType;
                        if (type != TextureType.Resource)
                        {
                            SimpleLog.Error($"{i}{j} was not a Resource texture");
                            continue;
                        };
#endif
                        var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                        var texFileNamePtr =
                            part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        // NOTE (Chiv) We are in a try-catch, so we just throw if the read failed.
                        // Cannot act anyways if the texture path is butchered
                        var textureFileName = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr))!;
                        //var success = uint.TryParse(textureFileName.Substring(textureFileName.LastIndexOf('/')+1, 6), out var iconId);

                        var _ = uint.TryParse(textureFileName.Substring(textureFileName.Length - 10, 6),
                            out var iconId);
                        //iconId = 0 (==> success == false as IconID will never be 0) Must have been NaviMap (and only that hopefully)
                        var textureIdPtr = new IntPtr(tex->D3D11ShaderResourceView);
                        Vector2 pMin;
                        Vector2 pMax;
                        var uv = zeroVec;
                        var uv1 = oneVec;
                        var tintColour = whiteColor;

                        switch (iconId)
                        {
                            case 0: //Arrows to quests and fates, glowy thingy
                                // NOTE (Chiv) We assume part.Width == part.Height == 24
                                // NOTE (Chiv) We assume tex.Width == 448 && tex.Height == 212
                                //TODO (Chiv) Will break on glowy under thingy, need to test if 'if' or 'read' is slower, I assume if
                                var u = (float) part.U / 448; // = (float) part.U / tex->Width;
                                var v = (float) part.V / 212; // = (float) part.V / tex->Height;
                                var u1 = (float) (part.U + 24) / 448; // = (float) (part.U + part.Width) / tex->Width;
                                var v1 = (float) (part.V + 24) / 212; // = (float) (part.V + part.Height) / tex->Height;
                                //var u = (float) part.U / tex->Width;
                                //var v = (float) part.V / tex->Height; 
                                //var u1 = (float) (part.U + part.Width) / tex->Width; 
                                //var v1 = (float) (part.V + part.Height) / tex->Height;
                                
                                uv = new Vector2(u, v);
                                uv1 = new Vector2(u1, v1);
                                goto case 1;
                            case 1: // Rotation Based icons go here after setting up their UVs
                                // TODO Ring, ring, SignedAngle first arg is FROM !
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                var rotationIconOffset = compassUnit *
                                                       -CalculateSignedAngle(mapIconComponentNode->AtkResNode.Rotation,
                                                           playerForward);
                                // We hope width == height
                                var rotationIconHalfWidth = part.Width * 0.5f * scaleFactorForRotationBasedDistance;
                                pMin = new Vector2(compassCentre.X - rotationIconHalfWidth + rotationIconOffset,
                                    compassCentre.Y - rotationIconHalfWidth);
                                pMax = new Vector2(
                                    compassCentre.X + rotationIconOffset + rotationIconHalfWidth,
                                    compassCentre.Y + rotationIconHalfWidth);
                                break;
                            case 060443: //Player Marker
                                if (!PluginConfig.ImGuiCompassEnableCenterMarker) continue;
                                drawList = backgroundDrawList;
                                pMin = new Vector2(compassCentre.X - halfWidth32,
                                    compassCentre.Y + PluginConfig.ImGuiCompassCentreMarkerOffset * scale -
                                    halfWidth32);
                                pMax = new Vector2(compassCentre.X + halfWidth32,
                                    compassCentre.Y + PluginConfig.ImGuiCompassCentreMarkerOffset * scale +
                                    halfWidth32);
                                uv1 = PluginConfig.ImGuiCompassFlipCentreMarker ? new Vector2(1, -1) : oneVec;
                                break;
                            case 060495: // Small Area Circle
                            case 060496: // Big Area Circle
                            case 060497: // Another Circle
                            case 060498: // One More Circle
                                bool inArea;
                                (pMin, pMax, tintColour, inArea)
                                    = CalculateAreaCirlceVariables(playerPos, playerForward, mapIconComponentNode,
                                        imgNode, mapScale, compassUnit, halfWidth32, compassCentre);
                                if (inArea)
                                    //*((byte*) &tintColour + 3) = 0x33  == (0x33FFFFFF) & (tintColour)
                                    backgroundDrawList.AddRectFilled(
                                        backgroundPMin
                                        , backgroundPMax
                                        , 0x33FFFFFF & tintColour //Set A to 0.2
                                        , PluginConfig.ImGuiCompassBackgroundRounding
                                    );
                                break;
                            case 060542: // Arrow UP on Circle
                            case 060546: // Arrow DOWN on Circle
                                (pMin, pMax, tintColour, _)
                                    = CalculateAreaCirlceVariables(playerPos, playerForward, mapIconComponentNode,
                                        imgNode, mapScale, compassUnit, halfWidth32, compassCentre);
                                break;
                            case 071023: // Quest Ongoing Marker
                            case 071025: // Quest Complete Marker
                            case 071063: // BookQuest Ongoing Marker
                            case 071065: // BookQuest Complete Marker
                            case 071083: // LeveQuest Ongoing Marker
                            case 071085: // LeveQuest Complete Marker
                            case 071143: // BlueQuest Ongoing Marker
                            case 071145: // BlueQuest Complete Marker
                                if (mapIconComponentNode->AtkResNode.Rotation == 0)
                                    goto default;
                                goto case 1; //No UV setup needed
                            case 060457: // Area Transition Bullet Thingy
                            default:
                                var (iconScaleFactor, iconAngle, _) = CalculateDrawVariables(
                                    playerPos,
                                    new Vector2(
                                        mapIconComponentNode->AtkResNode.X,
                                        mapIconComponentNode->AtkResNode.Y
                                    ),
                                    playerForward,
                                    mapScale);
                                // TODO Ring, ring, SignedAngle first arg is FROM !
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                // NOTE (Chiv) We assume part.Width == part.Height == 32
                                var iconOffset = compassUnit * -iconAngle;
                                var iconHalfWidth = halfWidth32 * iconScaleFactor;
                                pMin = new Vector2(compassCentre.X - iconHalfWidth + iconOffset,
                                    compassCentre.Y - iconHalfWidth);
                                pMax = new Vector2(
                                    compassCentre.X + iconOffset + iconHalfWidth,
                                    compassCentre.Y + iconHalfWidth);
                                break;
                        }

                        drawList.AddImage(textureIdPtr, pMin, pMax, uv, uv1, tintColour);
                    }
                }

                //TODO (Chiv) Later, we might do that for AreaMap too
            }
#if DEBUG
            catch (Exception e)
            {

                SimpleLog.Error(e);
#else
            catch
            {
                // ignored
#endif
            }
        }

        private static (Vector2 pMin, Vector2 pMax, uint tintColour, bool inArea) CalculateAreaCirlceVariables(
            Vector2 playerPos, Vector2 playerForward, AtkComponentNode* mapIconComponentNode,
            AtkImageNode* imgNode, float mapScale, float compassUnit, float halfWidth32, Vector2 compassCentre)
        {
            // TODO Distinguish between Circles for quests and circles for Fates (colour?)
            var (scaleArea, angleArea, distanceArea) = CalculateDrawVariables(
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
            // TODO Ring, ring, SignedAngle first arg is FROM !
            // TODO (Chiv) Ehhh, the minus before SignedAngle
            // NOTE (Chiv) We asumme part.Width == part.Height == 32
            var areaCircleOffset = compassUnit * -angleArea;
            var areaHalfWidth = halfWidth32 * scaleArea;
            if (distanceArea >= radius)
                return (
                    new Vector2(compassCentre.X - areaHalfWidth + areaCircleOffset, compassCentre.Y - areaHalfWidth),
                    new Vector2(compassCentre.X + areaCircleOffset + areaHalfWidth, compassCentre.Y + areaHalfWidth),
                    0xFFFFFFFF, false);
            var tintColour = ImGui.ColorConvertFloat4ToU32(new Vector4(
                (255 + imgNode->AtkResNode.AddRed) * (imgNode->AtkResNode.MultiplyRed / 100f) / 255f,
                (255 + imgNode->AtkResNode.AddGreen) * (imgNode->AtkResNode.MultiplyGreen / 100f) / 255f,
                (255 + imgNode->AtkResNode.AddBlue) * (imgNode->AtkResNode.MultiplyBlue / 100f) / 255f,
                1));
            return (new Vector2(compassCentre.X - areaHalfWidth, compassCentre.Y - areaHalfWidth)
                , new Vector2(compassCentre.X + areaHalfWidth, compassCentre.Y + areaHalfWidth)
                , tintColour, true);
        }

        //TODO Extract to Extensions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (float scaleFactor, float signedAngle, float distance) CalculateDrawVariables(Vector2 from,
            Vector2 to, Vector2 forward, float distanceScaling)
        {
            const float lowestScaleFactor = 0.3f;
            var distanceOffset = 40f * distanceScaling; //80f @Max Zoom(==2) _NaviMap
            var maxDistance = 180f * distanceScaling; //360f @Max Zoom(==2) _NaviMap
            //TODO (Chiv) Oh boy, check the math
            var distance = Vector2.Distance(to, from);
            var scaleFactor = Math.Max(1f - (distance - distanceOffset) / maxDistance, lowestScaleFactor);
            //return (scaleFactor,SignedAngle(to  - from, forward), distance);
            return (scaleFactor, SignedAngle(from - to, forward), distance);
        }

        //TODO Extract to Extensions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateSignedAngle(in float rotation, in Vector2 forward)
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

            var navimapPtr = PluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
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
                var scale = 1f / naviMap->Scale * PluginConfig.AddonCompassScale;
                var scaleFactorForRotationBasedDistance =
                    Math.Max(1f - distanceOffset / maxDistance, lowestScaleFactor);
                // TODO (Chiv) My math must be bogus somewhere, cause I need to do same things differently then math would say
                // I think my and the games coordinate system do not agree
                // TODO (Chiv) Redo the math for not locked _NaviMap (might be easier? Should be the same?)
                var playerCos = (float) Math.Cos(cameraRotationInRadian);
                var playerSin = (float) Math.Sin(cameraRotationInRadian);
                //TODO (Chiv) Uhm, actually with H=1, it should be new Vector(cos,sin); that breaks calculations though...
                // Is my Coordinate System the same as the games' minimap?
                var playerViewVector = new Vector2(-playerSin, playerCos);
                // TODO (Chiv) do it all in Radians
                var compassUnit = PluginConfig.AddonCompassWidth / 360f;
                //First, the background
                UiHelper.SetSize(_background, PluginConfig.AddonCompassWidth, 50);
                _background->PartId = (ushort) PluginConfig.AddonCompassBackgroundPartId;
                UiHelper.SetPosition(
                    _background,
                    PluginConfig.AddonCompassOffset.X - PluginConfig.AddonCompassWidth / 2f,
                    PluginConfig.AddonCompassOffset.Y);
                _background->AtkResNode.ScaleX = _background->AtkResNode.ScaleY = scale;
                UiHelper.SetVisible((AtkResNode*) _background, !PluginConfig.AddonCompassDisableBackground);
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
                    PluginConfig.AddonCompassOffset.X + compassUnit * -SignedAngle(east, playerViewVector),
                    PluginConfig.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[1],
                    PluginConfig.AddonCompassOffset.X + compassUnit * -SignedAngle(south, playerViewVector),
                    PluginConfig.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[2],
                    PluginConfig.AddonCompassOffset.X + compassUnit * -SignedAngle(west, playerViewVector),
                    PluginConfig.AddonCompassOffset.Y);
                UiHelper.SetPosition(
                    _cardinalsClonedImageNodes[3],
                    PluginConfig.AddonCompassOffset.X + compassUnit * -SignedAngle(north, playerViewVector),
                    PluginConfig.AddonCompassOffset.Y);

                _cardinalsClonedImageNodes[0]->AtkResNode.ScaleX =
                    _cardinalsClonedImageNodes[0]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[1]->AtkResNode.ScaleX =
                    _cardinalsClonedImageNodes[1]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[2]->AtkResNode.ScaleX =
                    _cardinalsClonedImageNodes[2]->AtkResNode.ScaleY = scale;
                _cardinalsClonedImageNodes[3]->AtkResNode.ScaleX =
                    _cardinalsClonedImageNodes[3]->AtkResNode.ScaleY = scale;

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
                        var imgNode = (AtkImageNode*) iconComponentNode->Component->ULDData.NodeList[j];
                        if (imgNode->AtkResNode.Type != NodeType.Image)
                        {
                            SimpleLog.Error($"{i}{j} was not an ImageNode");
                            continue;
                        }

                        clone->PartId = 0;

                        var show = iconComponentNode->AtkResNode.IsVisible && imgNode->AtkResNode.IsVisible;
                        if (!show)
                        {
                            // NOTE (Chiv) Shown + PartsList null explodes
                            UiHelper.Hide(clone);
                            clone->PartsList = null;
                            continue;
                        }

                        ;
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
                        }

                        ;
                        var texFileNamePtr =
                            part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                .FileName;
                        var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                        switch (texString)
                        {
                            case var _ when texString?.EndsWith("060443.tex",
                                StringComparison.InvariantCultureIgnoreCase) ?? false: //Player Marker
                                UiHelper.SetPosition(
                                    clone,
                                    PluginConfig.AddonCompassOffset.X,
                                    PluginConfig.AddonCompassOffset.Y);
                                break;
                            case var _ when texString?.EndsWith("060457.tex",
                                StringComparison.InvariantCultureIgnoreCase) ?? false: // Area Transition Bullet Thingy
                                var toObject = new Vector2(playerX - iconComponentNode->AtkResNode.X,
                                    playerY - iconComponentNode->AtkResNode.Y);
                                var distanceToObject = Vector2.Distance(
                                    new Vector2(iconComponentNode->AtkResNode.X, iconComponentNode->AtkResNode.Y),
                                    playerPos) - distanceOffset;
                                var scaleFactor = Math.Max(1f - distanceToObject / maxDistance, lowestScaleFactor);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale * scaleFactor;
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                UiHelper.SetPosition(
                                    clone,
                                    PluginConfig.AddonCompassOffset.X +
                                    compassUnit * -SignedAngle(toObject, playerViewVector),
                                    PluginConfig.AddonCompassOffset.Y);
                                break;
                            case var _ when (texString?.EndsWith("NaviMap.tex",
                                StringComparison.InvariantCultureIgnoreCase) ?? false) && imgNode->PartId == 21:
                                UiHelper.Hide(clone);
                                break;
                            case var _ when iconComponentNode->AtkResNode.Rotation == 0:
                                var toObject2 = new Vector2(playerX - iconComponentNode->AtkResNode.X,
                                    playerY - iconComponentNode->AtkResNode.Y);
                                var distanceToObject2 = Vector2.Distance(
                                    new Vector2(iconComponentNode->AtkResNode.X, iconComponentNode->AtkResNode.Y),
                                    playerPos) - distanceOffset;
                                var scaleFactor2 = Math.Max(1f - distanceToObject2 / maxDistance, lowestScaleFactor);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY = scale * scaleFactor2;
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                UiHelper.SetPosition(
                                    clone,
                                    PluginConfig.AddonCompassOffset.X +
                                    compassUnit * -SignedAngle(toObject2, playerViewVector),
                                    PluginConfig.AddonCompassOffset.Y);
                                break;
                            default:
                                var cosArrow = (float) Math.Cos(iconComponentNode->AtkResNode.Rotation);
                                var sinArrow = (float) Math.Sin(iconComponentNode->AtkResNode.Rotation);
                                // TODO (Chiv) Wrong again!
                                var toObject3 = new Vector2(-sinArrow, cosArrow);
                                clone->AtkResNode.ScaleX = clone->AtkResNode.ScaleY =
                                    scale * scaleFactorForRotationBasedDistance;
                                // TODO (Chiv) Ehhh, the minus before SignedAngle
                                UiHelper.SetPosition(
                                    clone,
                                    PluginConfig.AddonCompassOffset.X +
                                    compassUnit * -SignedAngle(toObject3, playerViewVector),
                                    PluginConfig.AddonCompassOffset.Y);
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
                var navimapPtr = PluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
                if (navimapPtr == IntPtr.Zero) return;
                var naviMap = (AtkUnitBase*) navimapPtr;
                //NOTE (chiv) 3 means fully loaded
                if (naviMap->ULDData.LoadedState != 3) return;
                //NOTE (chiv) There should be no real need to care for visibility for cloning but oh well, untested.
                if (!naviMap->IsVisible) return;
                //NOTE (chiv) 19 should be the unmodified default
                //if (naviMap->ULDData.NodeListCount != 19) return;

                UiHelper.ExpandNodeList(naviMap, (ushort) _clonedImageNodes.Length);
                var prototypeImgNode = naviMap->ULDData.NodeList[11];
                var currentNodeId = uint.MaxValue;

                //First, Background (Reusing empty slot)
                _background = (AtkImageNode*) CloneAndAttach(
                    ref naviMap->ULDData,
                    naviMap->RootNode,
                    prototypeImgNode,
                    currentNodeId--);
                _background->WrapMode = 2;
                if (PluginConfig.AddonCompassDisableBackground) UiHelper.Hide(_background);

                // Next, Cardinals
                for (var i = 0; i < _cardinalsClonedImageNodes.Length; i++)
                    _cardinalsClonedImageNodes[i] = (AtkImageNode*) CloneAndAttach(
                        ref naviMap->ULDData,
                        naviMap->RootNode,
                        naviMap->ULDData.NodeList[i + 9],
                        currentNodeId--);

                // NOTE (Chiv) Set for rest
                // This is overkill but we are basically mimicking the amount of nodes in _NaviMap.
                // It could be that all are used!
                for (var i = 0; i < _clonedImageNodes.GetLength(0); i++)
                for (var j = 0; j < _clonedImageNodes.GetLength(1); j++)
                {
                    _clonedImageNodes[i, j] = (AtkImageNode*) CloneAndAttach(
                        ref naviMap->ULDData,
                        naviMap->RootNode,
                        prototypeImgNode,
                        currentNodeId--);
                    // Explodes if PartsList = null before Hiding it
                    UiHelper.Hide(_clonedImageNodes[i, j]);
                    _clonedImageNodes[i, j]->PartId = 0;
                    _clonedImageNodes[i, j]->PartsList = null;
                }

                //TODO (Chiv) Search for PlayerIcon and set it up too?

                PluginInterface.Framework.OnUpdateEvent -= OnFrameworkUpdateSetupAddonNodes;
                PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdateUpdateAddonCompass;
            }
            catch (Exception e)
            {
                SimpleLog.Error(e);
            }
        }

        private static AtkResNode* CloneAndAttach(ref ULDData uld, AtkResNode* parent, AtkResNode* prototype,
            uint nodeId = uint.MaxValue)
        {
            var clone = UiHelper.CloneNode(prototype);
            clone->NodeID = nodeId;
            clone->ParentNode = parent;
            var currentLastSibling = parent->ChildNode;
            while (currentLastSibling->PrevSiblingNode != null)
                currentLastSibling = currentLastSibling->PrevSiblingNode;
            clone->PrevSiblingNode = null;
            clone->NextSiblingNode = currentLastSibling;
            currentLastSibling->PrevSiblingNode = clone;
            uld.NodeList[uld.NodeListCount++] = clone;
            return clone;
        }
        
        
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

        #region UI

        private void BuildUi()
        {
            BuildImGuiCompass();
            if (!_buildingConfigUi) return;
            var (shouldBuildConfigUi, changedConfig) = ConfigurationUi.DrawConfigUi(PluginConfig);
            if (changedConfig)
                PluginInterface.SavePluginConfig(PluginConfig);

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
                PluginInterface.ClientState.OnLogin -= OnLogin;
                PluginInterface.ClientState.OnLogout -= OnLogout;
                PluginInterface.CommandManager.RemoveHandler(Command);

                _clampToMinusPiAndPi?.Disable();
                _clampToMinusPiAndPi?.Dispose();
                _setCameraRotation?.Disable();
                _setCameraRotation?.Dispose();

                _compassImage?.Dispose();
                _naviMap?.Dispose();
#if DEBUG
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