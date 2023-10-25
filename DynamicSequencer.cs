using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

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
        public static readonly string projectDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DynamicSequencer", "Projects");

        public static string previousProject = "";
        public static string previousTarget = "";
        public static Dictionary<string, int> ditherLog = new Dictionary<string, int>();

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

            Directory.CreateDirectory(projectDir);
        }
    }
}
