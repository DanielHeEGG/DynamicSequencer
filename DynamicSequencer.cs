using System.ComponentModel.Composition;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;

using Settings = DanielHeEGG.NINA.DynamicSequencer.Properties.Settings;

namespace DanielHeEGG.NINA.DynamicSequencer
{
    [Export(typeof(IPluginManifest))]
    public class DynamicSequencer : PluginBase
    {
        public static PProject previousProject = null;
        public static PTarget previousTarget = null;

        private readonly IProfileService _profileService;

        [ImportingConstructor]
        public DynamicSequencer(IProfileService profileService)
        {
            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            _profileService = profileService;
        }
    }
}
