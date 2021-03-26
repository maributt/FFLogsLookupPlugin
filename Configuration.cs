using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace FFLogsLookup
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }
        public bool ShowNormal = false;
        public bool ShowUltimates = false;
        public int OffsetX;
        public int OffsetY;
        public bool ShowBackground = false;
        public bool ShowOnlyNormal = false;

        public string client_id = "";
        public string client_secret = "";
        public string bearer_token;

        public bool initialConfig = true;

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
