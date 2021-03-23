using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace FFLogsLookup
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }
        public bool ShowNormal { get; set; }
        public bool ShowUltimates { get; set; }
        public int OffsetX { get; set; } = 73;
        public int OffsetY { get; set; } = -120;

        // Add any other properties or methods here.
        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
