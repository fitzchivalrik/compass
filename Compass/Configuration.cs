using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using SimpleTweaksPlugin.Debugging;

namespace Compass
{
    public class Configuration : IPluginConfiguration
    {
#if DEBUG
        public DebugConfig Debugging = new();
#endif
        public Vector2 AddonCompassOffset = new(0,0);
        public float AddonCompassScale = 1f;
        public int AddonCompassWidth = 550;
        // NOTE (Chiv) ImGuiCompass Offset/Position are saved as the window coords/size.
        public int AddonCompassBackgroundPartId = 1;
        public bool AddonCompassDisableBackground;
        public bool AddonCompassEnable = true;
        
        public float ImGuiCompassScale = 1f;
        public int ImGuiCompassBackgroundPartId = 1;
        public bool ImGuiCompassDisableBackground;
        public bool ImGuiCompassEnable;
        
        public int Version { get; set; } = 0;
    }
    
}