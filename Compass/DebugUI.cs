using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors;
using FFXIVClientStructs.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.Debugging;
using static Compass.Extensions;

#if DEBUG
namespace Compass
{
    public partial class Compass
    {
        const int COMPASS_STR_LEN = 38;
        const float TOLERANCE = 0.00001f;
        private static readonly Vector4 CARDINAL_COL = new Vector4(1, 0, 1, 1);
        private static readonly Vector4 INDICATOR_LONG_COL = new Vector4(1,1,1,1);
        private static readonly Vector4 INDICATOR_SHORT_COL = new Vector4(0.4f,0.4f,0.4f,1);
        private char[] cardinals_str = new char[COMPASS_STR_LEN];
        private char[] indicator_long_str = new char[COMPASS_STR_LEN];
        private char[] indicator_short_str = new char[COMPASS_STR_LEN];
        private float compass_inc_size = 5f;
        private float _lastSeenAngle = 181f;

        private bool _refresh = true;
        private UIDebug _UIDebug;
        private bool _recalculate;
        private bool _destroy;
        private bool _replaceTexture;
        private int _partId;
        private int _wrapMode;
        private int _width;
        private int _height;
        private float _rotation;
        private List<(float radius, float distance)> _areaCircle = new();

        private unsafe void CompassExploration()
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.None;
            var open = true;
            
            
            //ImGui.SetNextWindowPos(ImVec2((float)x, (float)y), ImGuiSetCond_FirstUseEver);
            if (!ImGui.Begin("Compass EXploration##Compass!!!", ref open, flags))
            {
                ImGui.End();
                return;
            }

            
            var naviMap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            
            var addonName = Marshal.PtrToStringAnsi(new IntPtr(naviMap->Name));
            var miniMapRootComponentNode = (AtkComponentNode*)naviMap->ULDData.NodeList[2];
            // TODO (Chiv) Getting once in framework, stays valid? On Zone changes too?
            var miniMapCameraTriangleRotation = miniMapRootComponentNode->Component->ULDData.NodeList[2];

            ImGui.Text("MiniMapRotation " + miniMapCameraTriangleRotation->Rotation);
            var miniMapCameraTriangleImgNode = (AtkImageNode*)miniMapCameraTriangleRotation->ChildNode;
            var uldPart = miniMapCameraTriangleImgNode->PartsList->Parts[miniMapCameraTriangleImgNode->PartId];
            ImGui.Text($"{uldPart.U} {uldPart.V} {uldPart.Width} {uldPart.Height}");
            var textureInfo = uldPart.ULDTexture;
            var texType = textureInfo->AtkTexture.TextureType;
            
            switch (texType)
            {
                case TextureType.Resource:
                {
                    var texFileNamePtr = textureInfo->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName;
                    var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                    //ImGui.Text($"texture path: {texString}");
                    var kernelTexture = textureInfo->AtkTexture.Resource->KernelTextureObject;

                    var u = (float)uldPart.U / kernelTexture->Width;
                    var v = (float)uldPart.V / kernelTexture->Height;
                    var u1 = (float)(uldPart.U + uldPart.Width) / kernelTexture->Width;
                    var v1 = (float)(uldPart.V + uldPart.Height) / kernelTexture->Height;
                    
                    
                    //ImGui.Text($"{u} {v} {u1} {v1}");
                    if (ImGui.TreeNode($"Texture##{(ulong) kernelTexture->D3D11ShaderResourceView:X}")) {
                        
                        var pos = ImGui.GetCursorScreenPos();
                        var marker_min = new Vector2(pos.X, pos.Y);
                        var marker_max = new Vector2(pos.X+ uldPart.Width, pos.Y + uldPart.Height);
                        
                        
                        
                        ImGui.GetWindowDrawList()
                            .AddImage(
                            new IntPtr(kernelTexture->D3D11ShaderResourceView),
                            marker_min,
                            marker_max,
                            new Vector2(u, v),
                            new Vector2(u1,v1)
                            );
                        ImGui.Dummy(new Vector2(uldPart.Width, uldPart.Height));
                        pos = ImGui.GetCursorScreenPos();
                        marker_min = new Vector2(pos.X, pos.Y);
                        marker_max = new Vector2(pos.X+ uldPart.Width, pos.Y + uldPart.Height);
                        ImGui.GetWindowDrawList()
                            .AddImageRounded(
                                new IntPtr(kernelTexture->D3D11ShaderResourceView),
                                marker_min,
                                marker_max,
                                new Vector2(0, 0),
                                new Vector2(1, 1),
                                ImGui.ColorConvertFloat4ToU32(new Vector4(1f)),
                                0.5f,
                                ImDrawCornerFlags.All
                                );
                        ImGui.Dummy(new Vector2(uldPart.Width, uldPart.Height));
                        
                        pos = ImGui.GetCursorScreenPos();
                        var angle = miniMapCameraTriangleRotation->Rotation + miniMapCameraTriangleImgNode->AtkResNode.Rotation;
                        ImageRotated(
                            new IntPtr(kernelTexture-> D3D11ShaderResourceView),
                            new Vector2(pos.X + uldPart.Width/2f, pos.Y + uldPart.Height/2f),
                            new Vector2(uldPart.Width, uldPart.Height),
                            angle,
                            new Vector2(u, v),
                            new Vector2(u1, v1)
                        ); 
                        ImGui.Dummy(new Vector2(uldPart.Width, uldPart.Height));
                        /*
                        ImGui.GetWindowDrawList()
                            .AddRectFilled(
                                marker_min,
                                marker_max,
                                ImGui.ColorConvertFloat4ToU32(new Vector4(255, 0, 255, 255)));
                                */
                        /*
                        ImGui.Image(
                            new IntPtr(kernelTexture->D3D11ShaderResourceView),
                            new Vector2(uldPart.Width, uldPart.Height) *  ImGui.GetIO().FontGlobalScale,
                            new Vector2(u, v),
                            new Vector2(u1,v1),
                            new Vector4(1,0,1,1f),
                            new Vector4(1,1,1,1)
                            );
                          */  
                        
                        ImGui.TreePop();
                    }

                    break;
                }
                case TextureType.KernelTexture:
                {
                    if (ImGui.TreeNode($"Texture##{(ulong) textureInfo->AtkTexture.KernelTexture->D3D11ShaderResourceView:X}")) {
                        ImGui.Image(new IntPtr(textureInfo->AtkTexture.KernelTexture->D3D11ShaderResourceView), new Vector2(textureInfo->AtkTexture.KernelTexture->Width, textureInfo->AtkTexture.KernelTexture->Height)); 
                        ImGui.TreePop();
                    }

                    break;
                }
            }
            
            ImGui.Separator();
            ImGui.Text($"MaybeCameraStruct {_maybeCameraStruct.ToString("X")}");
            ImGui.Text($"Camera Base {_cameraBase.ToString("X")}");
            ImGui.Text($"SceneCameraObject {_sceneCameraObject.ToString("X")}");
            ImGui.Text($"GameCameraObject {_gameCameraObject.ToString("X")}");
            ImGui.Text($"CameraManager {_cameraManager.ToString("X")}");
            ImGui.Separator();
            var cameraRotationInRadian = -*(float*) (_maybeCameraStruct + 0x130);
            var cameraRotationInDegree = (cameraRotationInRadian) * Rad2Deg;
            var startAngle = cameraRotationInDegree - (COMPASS_STR_LEN / 2f) * compass_inc_size;
            if (startAngle < -180) {
                startAngle += 360;
            }
            var roundedAngle = RoundUpToMultipleOf(startAngle, compass_inc_size);
            ImGui.Text("Rotation "+ -cameraRotationInRadian);
            ImGui.Text("Rotation "+ -cameraRotationInDegree);
            ImGui.Text("StartAngle "+startAngle);
            ImGui.Text("Rounded Angle "+roundedAngle);
            
            // redraw the strings only if cam changed
            if (Math.Abs(_lastSeenAngle - 181.00f) < TOLERANCE ||
                Math.Abs(_lastSeenAngle - cameraRotationInDegree) > TOLERANCE)
            {
                _lastSeenAngle = cameraRotationInDegree;
                // reset the current compass strings
                Fill(cardinals_str, ' ');
                Fill(indicator_long_str, ' ');
                Fill(indicator_short_str, ' ');
                
                // round up to nearest compass inc multiple
                // and adjust the cursor relative to starting position
                var curAngle = roundedAngle;
                for (var i = 0; i < COMPASS_STR_LEN; i++)
                {
                    var nextAngle = curAngle + compass_inc_size;
                    if (nextAngle > 180) {
                        nextAngle -= 360;
                    }

                    if (Math.Abs(Math.Abs(curAngle) - 180.00) < TOLERANCE) {
                        cardinals_str[i] = 'S';
                    }
                    else if (Math.Abs(curAngle - (-90.00)) < TOLERANCE) {
                        cardinals_str[i] = 'W';
                    }
                    else if (curAngle == 0.00) {
                        cardinals_str[i] = 'N';
                    }
                    else if (Math.Abs(curAngle - 90.00) < TOLERANCE) {
                        cardinals_str[i] = 'E';
                    }
                    else if (curAngle % (compass_inc_size * 3) == 0) {
                        indicator_long_str[i] = '|';
                    }
                    else {
                        indicator_short_str[i] = '|';
                    }

                    curAngle = nextAngle;
                }
            }
            //ImGui.SameLine(1);
            foreach (var c in cardinals_str)
            {
                //ImGui.TextColored( CARDINAL_COL,""+c);
                ImGui.Text(""+c);
                ImGui.SameLine();
            }
            ImGui.Dummy(Vector2.Zero);
            /*
            ImGui.SameLine(1);
            foreach (var c in indicator_long_str)
            {
                ImGui.TextColored( INDICATOR_LONG_COL,""+c);
                ImGui.SameLine();
            }
            ImGui.Dummy(Vector2.Zero);
            
            ImGui.SameLine(1);
            foreach (var c in indicator_short_str)
            {
                ImGui.TextColored( INDICATOR_SHORT_COL,""+c);
                ImGui.SameLine();
            }
            ImGui.Dummy(Vector2.Zero);
            */
            //ImGui.TextColored(CARDINAL_COL, new string(cardinals_str)); ImGui.SameLine(1);
            //ImGui.TextColored(INDICATOR_LONG_COL, new string(indicator_long_str)); ImGui.SameLine(1);
            //ImGui.TextColored(INDICATOR_SHORT_COL, new string(indicator_short_str));

            var p = ImGui.GetCursorPos();
            //ImGui.SameLine(1);
            //var p2 = ImGui.GetCursorPos();
            //ImGui.Dummy(Vector2.One);
            //ImGui.Text($"{p.X} {p.Y}");
            //ImGui.Text($"{p2.X} {p2.Y}");
            
            
            for (var i = 4; i < miniMapRootComponentNode->Component->ULDData.NodeListCount; i++)
            {
                var baseComponentNode = (AtkComponentNode*)miniMapRootComponentNode->Component->ULDData.NodeList[i];
                if (baseComponentNode->AtkResNode.IsVisible)
                {
                    for (var j = 2; j < baseComponentNode->Component->ULDData.NodeListCount; j++)
                    {
                        var node = baseComponentNode->Component->ULDData.NodeList[j];
                        if (node->IsVisible && node->Type == NodeType.Image)
                        {
                            var imgNode = (AtkImageNode*) node;
                            var part = imgNode->PartsList->Parts[imgNode->PartId];
                            var type = part.ULDTexture->AtkTexture.TextureType;
                            if (type == TextureType.Resource)
                            {
                                var texFileNamePtr =
                                    part.ULDTexture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle
                                        .FileName;
                                var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));
                                //ImGui.Text($"Texture Path {texString}");
                                if (texString?.EndsWith("060311.tex") ?? false)
                                {
                                    
                                    var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                                    var u = (float)part.U / tex->Width;
                                    var v = (float)part.V / tex->Height;
                                    var u1 = (float)(part.U + part.Width) / tex->Width;
                                    var v1 = (float)(part.V + part.Height) / tex->Height;
                                    var x = baseComponentNode->AtkResNode.X;
                                    var y = baseComponentNode->AtkResNode.Y;
                                    var playerX = 72;
                                    var playerY = 72;
                                    var cos = (float)Math.Cos(cameraRotationInRadian);
                                    var sin = (float)Math.Sin(cameraRotationInRadian);
                                    var playerViewVector =
                                        Vector2.UnitY.Rotate(
                                            cos,
                                            sin);
                                    //cos(a) -sin(a) 0
                                    //sin(a) cos(a) 0
                                    //0 0 1
                                    var rotationMatrix = new Matrix3x2(cos, -sin, sin, cos, 0, 1); ;
                                    var dot = Vector2.Dot(Vector2.Normalize(new Vector2(playerX - x, playerY - y)), playerViewVector);
                                    var playerViewVector2 = new Vector2(-sin, cos);
                                    var widthOfCompass = 410f;
                                    var compassUnit = widthOfCompass / 360f;
                                    var toObject = new Vector2(playerX -x, playerY - y);
                                    
                                    
                                    var signedAngle = ((float)Math.Atan2(toObject.Y, toObject.X) - (float)Math.Atan2(playerViewVector.Y, playerViewVector.X));
                                    var signedAngle2 = SignedAngle(toObject, playerViewVector2);
                                    //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                                    var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                                    ImGui.SameLine(205+compassPos.X);
                                    ImGui.Image(
                                        new IntPtr(tex->D3D11ShaderResourceView),
                                        new Vector2(part.Width, part.Height) *  ImGui.GetIO().FontGlobalScale,
                                        new Vector2(u, v),
                                        new Vector2(u1,v1)
                                        
                                        
                                    );
                                    ImGui.Text($"Found IT {x} {y}");
                                    ImGui.Text($"PlayerViewVector {playerViewVector}");
                                    ImGui.Text($"PlayerViewVector2 {playerViewVector2}");
                                    ImGui.Text($"Dot {dot}");
                                    ImGui.Text($"Compass Unit {compassUnit}");
                                    ImGui.Text($"Signed Angle: {signedAngle}/{signedAngle * Rad2Deg}");
                                    ImGui.Text($"Signed Angle2: {signedAngle2 * Deg2Rad}/{signedAngle2}");
                                    ImGui.Text($"CompassPos: {compassPos}");
                                }
                            }

                            //ImGui.Text($"{imgNode->AtkResNode.NodeID}");
                        }
                    }
                }
            }
            
            
            ImGui.End();
            
            var pluginInterfaceType = _pluginInterface.GetType();
            var dalamud = (Dalamud.Dalamud)pluginInterfaceType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .First(it => it.Name == "dalamud").GetValue(_pluginInterface)!;
            var dalamudUi = dalamud.GetType()
                .GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                .First(it => it.Name == "DalamudUi")
                .GetValue(dalamud)!;
            var dataWindow = dalamudUi.GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .First(it => it.Name == "dataWindow")
                .GetValue(dalamudUi)!;
            //dataWindow.GetType().GetMethod("Draw")!.Invoke(dataWindow, new object[0]);
        }
        private unsafe void BuildDebugUi()
        {
            
            CompassExploration();
            ImGui.SetNextWindowBgAlpha(1);
            if(!ImGui.Begin($"{PluginName} Debug")) { ImGui.End(); return;}

            foreach (var circle in _areaCircle)
            {
                ImGui.Text($"Radius {circle.radius} {circle.distance} Distance");
            }
            ImGui.Separator();
            ImGui.DragFloat2("Compass Offset", ref _compassOffset);
            ImGui.Separator();
            ImGui.InputFloat("Rotation", ref _rotation);
            ImGui.InputInt("PartId", ref _partId);
            ImGui.InputInt("WrapMode", ref _wrapMode);
            ImGui.InputInt("Width", ref _width);
            ImGui.InputInt("Height", ref _height);
            ImGui.InputFloat("Scale##compass scale", ref _scale);
            if (ImGui.Checkbox("replace", ref _replaceTexture))
            {
                
            }ImGui.SameLine();
            if (ImGui.Checkbox("Destroy", ref _destroy))
            {
                
            }

            ImGui.SameLine();
            if (ImGui.Checkbox("REcalculate Position", ref _recalculate))
            {
                
            }
            ImGui.SameLine();
            if(ImGui.Checkbox("Refresh", ref _refresh))
            {
                
            }
            ImGui.SameLine();
            if(ImGui.Checkbox("REset", ref _reset))
            {
                
            }
            ImGui.Separator();
            if (_clonedUnitBase != null)
            {
                _UIDebug.DrawUnitBase(_clonedUnitBase);
                
            }
            ImGui.Separator();
            ImGui.Separator();
            if (_clonedTxtNode != null)
            {
                _UIDebug.PrintNode((AtkResNode*)_clonedTxtNode, false);
            }
            ImGui.Separator();
            _UIDebug.Draw();
            var naviMap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("_NaviMap", 1);
            var areaMap =  (AtkUnitBase*) _pluginInterface.Framework.Gui.GetUiObjectByName("AreaMap", 1);
            ImGui.Text($"LoadedState of _NaviMap {naviMap->ULDData.LoadedState}");
            ImGui.Text($"LoadedState of AreaMap {areaMap->ULDData.LoadedState}");
            ImGui.Separator();
            
            ImGui.Text($"WindowSize {ImGui.GetWindowSize()}");
            ImGui.Text($"Window Width {ImGui.GetWindowWidth()}");
            ImGui.Text($"Window Height {ImGui.GetWindowHeight()}");
            ImGui.Text($"Window Content Region Width {ImGui.GetWindowContentRegionWidth()}");
            ImGui.Text($"ContentRegionVail {ImGui.GetContentRegionAvail()}");
            ImGui.Separator();
            
            ImGui.Image(_naviMap.ImGuiHandle, new Vector2(_naviMap.Width, _naviMap.Height));
            
            ImGui.Separator();
            var miniMapRootComponentNode = (AtkComponentNode*)naviMap->ULDData.NodeList[2];
            for (var i = int.MaxValue; i < miniMapRootComponentNode->Component->ULDData.NodeListCount; i++)
            {
                var baseComponentNode = (AtkComponentNode*) miniMapRootComponentNode->Component->ULDData.NodeList[i];
                if (!baseComponentNode->AtkResNode.IsVisible) continue;
                for (var j = int.MaxValue; j < baseComponentNode->Component->ULDData.NodeListCount; j++)
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
                    if (
                        (texString?.EndsWith("071025.tex") ?? false) 
                        || (texString?.EndsWith("060955.tex") ?? false)
                        || (texString?.EndsWith("071021.tex") ?? false)
                        //|| ((texString?.EndsWith("navimap.tex") ?? false) && imgNode->PartId == 21 ) 
                        )
                    {
                        //imgNode->PartId = 0;
                        var tex = part.ULDTexture->AtkTexture.Resource->KernelTextureObject;
                        var u = (float)part.U / tex->Width;
                        var v = (float)part.V / tex->Height;
                        var u1 = (float)(part.U + part.Width) / tex->Width;
                        var v1 = (float)(part.V + part.Height) / tex->Height;
                        var x = baseComponentNode->AtkResNode.X;
                        var y = baseComponentNode->AtkResNode.Y;
                        var playerX = 72;
                        var playerY = 72;
                        //var cos = (float)Math.Cos(cameraRotationInRadian);
                        //var sin = (float)Math.Sin(cameraRotationInRadian);
                        //var playerViewVector =
                        //    Vector2.UnitY.Rotate(
                        //        cos,
                        //        sin);
                        //var dot = Vector2.Dot(Vector2.Normalize(new Vector2(playerX - x, playerY - y)), playerViewVector);
                        //var playerViewVector2 = new Vector2(-sin, cos);
                        var widthOfCompass = _compassImage.Width;
                        var compassUnit = widthOfCompass / 360f;
                        var toObject = new Vector2(playerX -x, playerY - y);
                                    
                        
                        ImGui.Text($"{part.U} {part.V} {part.Width} {part.Height}");            
                        ImGui.Text($"{tex->Width} {tex->Width}");            
                        ImGui.Text($"{u} {v} {u1} {v1}");            
                        //var signedAngle = ((float)Math.Atan2(toObject.Y, toObject.X) - (float)Math.Atan2(playerViewVector.Y, playerViewVector.X));
                        //var signedAngle2 = -SignedAngle(toObject, playerViewVector2);
                        //if (signedAngle > Math.PI) signedAngle -= 2f * (float) Math.PI;
                        //var compassPos = new Vector2(compassUnit * signedAngle2, 0f);
                        //ImGui.SameLine(_compassImage.Width/2f+compassPos.X);
                        ImGui.Image(
                            new IntPtr(tex->D3D11ShaderResourceView),
                            new Vector2(part.Width, part.Height) *  ImGui.GetIO().FontGlobalScale
                            , imgNode->PartId == 0 ? new Vector2(0,0) : new Vector2(u, v)
                            ,imgNode->PartId == 0 ? new Vector2(1,1) : new Vector2(u1, v1)
                                        
                                        
                        );
                    }
                }
            }
            ImGui.Separator();
            ImGui.Text($"Cardinals Str");
            foreach (var c in cardinals_str)
            {
                ImGui.Text(""+c);
                ImGui.SameLine();
            }
            ImGui.Dummy(Vector2.Zero);
            ImGui.Text($"Long Indicator Str");
            foreach (var c in indicator_long_str)
            {
                ImGui.Text(""+c);
                ImGui.SameLine();
            }
            ImGui.Dummy(Vector2.Zero);
            ImGui.Text($"ShortIndicator Str");
            foreach (var c in indicator_short_str)
            {
                ImGui.Text(""+c);
                ImGui.SameLine();
            }
            ImGui.Dummy(Vector2.Zero);
            
            
            
            ImGui.End();
        }

    }
}
#endif