using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

using Newtonsoft.Json;

using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;

using Serilog;

using Settings = DanielHeEGG.NINA.DynamicSequencer.Properties.Settings;

namespace DanielHeEGG.NINA.DynamicSequencer
{
    [Export(typeof(IPluginManifest))]
    public class DynamicSequencer : PluginBase
    {
        public static readonly string pluginDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DynamicSequencer");
        public static readonly string projectDir = Path.Combine(pluginDir, "Projects");
        public static readonly string logDir = Path.Combine(pluginDir, "Logs");

        public static readonly string settingsFile = Path.Combine(pluginDir, "settings.json");

        public static string previousProject = "";
        public static string previousTarget = "";
        public static Dictionary<string, int> ditherLog = new Dictionary<string, int>();

        public static ILogger logger;
        public static PluginSettings pluginSettings;

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
            Directory.CreateDirectory(logDir);

            if (File.Exists(settingsFile))
            {
                using (StreamReader r = File.OpenText(settingsFile))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    pluginSettings = (PluginSettings)serializer.Deserialize(r, typeof(PluginSettings));
                }
            }
            else
            {
                pluginSettings = new PluginSettings();
                using (StreamWriter w = new StreamWriter(settingsFile))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(w, pluginSettings);
                }
            }

            LoggerConfiguration loggerConfig = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(Path.Combine(logDir, "log-.txt"), rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information);
            logger = pluginSettings.logDebug ? loggerConfig.WriteTo.File(Path.Combine(logDir, "debug_log-.txt"), rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug).CreateLogger() : loggerConfig.CreateLogger();
        }

        public override Task Teardown()
        {
            (logger as IDisposable).Dispose();
            return base.Teardown();
        }
    }
}
