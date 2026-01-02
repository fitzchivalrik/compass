using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Compass.Data;

internal readonly unsafe struct Pointers
{
    internal readonly float*            PlayerViewTriangleRotation;
    internal readonly AtkUnitBase*      CurrentSourceBase;
    internal readonly AtkComponentNode* CurrentMapIconsRootComponentNode;
    internal readonly AtkImageNode*     WeatherIconNode;
    internal readonly TargetSystem*     TargetSystem;
    internal readonly ImTextureID       NaviMapTextureD3D11ShaderResourceView;

    public Pointers(
        TargetSystem*     targetSystem
      , float*            playerViewTriangleRotation
      , AtkUnitBase*      currentSourceBase
      , AtkComponentNode* currentMapIconsRootComponentNode
      , AtkImageNode*     weatherIconNode
      , void*              naviMapTextureD3D11ShaderResourceView
    )
    {
        TargetSystem                          = targetSystem;
        WeatherIconNode                       = weatherIconNode;
        NaviMapTextureD3D11ShaderResourceView = new ImTextureID(naviMapTextureD3D11ShaderResourceView);
        CurrentMapIconsRootComponentNode      = currentMapIconsRootComponentNode;
        CurrentSourceBase                     = currentSourceBase;
        PlayerViewTriangleRotation            = playerViewTriangleRotation;
    }
}