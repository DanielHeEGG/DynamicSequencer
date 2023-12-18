using System.Collections.Generic;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using Newtonsoft.Json;

namespace DanielHeEGG.NINA.DynamicSequencer
{
    [JsonObject(MemberSerialization.OptOut)]
    public class PluginSettings
    {
        public bool logDebug { get; set; } = false;
        public List<PProjectSelectionPriority> projectSelectionPriority { get; set; } = [PProjectSelectionPriority.PRIORITY, PProjectSelectionPriority.COMPLETION];
    }
}
