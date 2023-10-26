using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

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
        public static readonly string projectDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DynamicSequencer", "Projects");
        public static readonly string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DynamicSequencer", "Logs");
        public static readonly string debugLogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DynamicSequencer", "Debug Logs");

        public static string previousProject = "";
        public static string previousTarget = "";
        public static Dictionary<string, int> ditherLog = new Dictionary<string, int>();

        public static ILogger logger;

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
            Directory.CreateDirectory(debugLogDir);

            logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(logDir, "log-.txt"), rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .WriteTo.File(Path.Combine(debugLogDir, "debug_log-.txt"), rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug)
            .CreateLogger();
        }

        public override Task Teardown()
        {
            (logger as IDisposable).Dispose();
            return base.Teardown();
        }
    }
}
