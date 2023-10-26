using System.ComponentModel;

using Newtonsoft.Json;

namespace DanielHeEGG.NINA.DynamicSequencer
{
    [JsonObject(MemberSerialization.OptOut)]
    public class PluginSettings
    {
        [DefaultValue(false)]
        public bool logDebug { get; set; }
    }
}
