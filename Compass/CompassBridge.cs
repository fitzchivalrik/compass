using System;
using Dalamud.Plugin;

namespace Compass
{
    public class CompassBridge : IDalamudPlugin
    {
        private Compass _plugin = null!;
        public string Name => Compass.PluginName;
        
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
    }
}