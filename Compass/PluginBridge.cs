using System;
using Dalamud.Plugin;

namespace Compass
{
    public class PluginBridge : IDalamudPlugin
    {
        private Compass _plugin = null!;
        public string Name => Compass.PluginName;
        
        
        //NOTE (Chiv) For LPL to set the correct AssemblyLocation
        public static string AssemblyLocation { get; private set; } = System.Reflection.Assembly.GetExecutingAssembly().Location;
        
        public void Initialize(DalamudPluginInterface pi)
        {
            var config = pi.GetPluginConfig() as Configuration ?? new Configuration();
            _plugin = new Compass(pi, config);
        }
        
        public void Dispose()
        {
            _plugin.Dispose();
            GC.SuppressFinalize(this);
        }
        
#if DEBUG
        //NOTE (Chiv) For LPL to set the correct AssemblyLocation
        private void SetLocation(string s)
        {
            AssemblyLocation = s;
        }
#endif
    }
}