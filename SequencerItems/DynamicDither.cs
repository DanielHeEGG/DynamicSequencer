using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Dither")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "DitherSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Guider")]
    [Export(typeof(ISequenceItem))]
    public class DynamicDither : SequenceItem, IValidatable
    {
        private IProfileService _profileService;
        private IGuiderMediator _guiderMediator;

        private IList<string> issues = new List<string>();
        public IList<string> Issues
        {
            get => issues;
            set
            {
                issues = value;
                RaisePropertyChanged();
            }
        }

        [ImportingConstructor]
        public DynamicDither(IProfileService profileService, IGuiderMediator guiderMediator)
        {
            _profileService = profileService;
            _guiderMediator = guiderMediator;
        }

        private DynamicDither(DynamicDither cloneMe) : this(cloneMe._profileService, cloneMe._guiderMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new DynamicDither(this);
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            DynamicSequencer.logger.Debug("Dither: execute");

            var planner = new Planner();
            planner.Filter(_profileService);
            var project = planner.GetProjectFromString(DynamicSequencer.currentProject);
            if (project == null || !project.valid)
            {
                DynamicSequencer.logger.Information("Dither: current project not valid, skipped");
                return Task.CompletedTask;
            }
            var target = project.getTargetFromString(DynamicSequencer.currentTarget);
            if (target == null || !target.valid)
            {
                DynamicSequencer.logger.Information("Dither: current target not valid, skipped");
                return Task.CompletedTask;
            }
            var exposure = target.Best();
            if (exposure == null)
            {
                DynamicSequencer.logger.Information("Dither: no valid exposure, skipped");
                return Task.CompletedTask;
            }

            if (project.ditherEvery <= 0)
            {
                DynamicSequencer.logger.Information("Dither: project disabled dither, skipped");
                return Task.CompletedTask;
            }

            if (!DynamicSequencer.ditherLog.ContainsKey(exposure.ToString()))
            {
                DynamicSequencer.logger.Information("Dither: exposure not in ditherLog, skipped");
                return Task.CompletedTask;
            }

            int exposureCount = DynamicSequencer.ditherLog[exposure.ToString()];
            if (exposureCount < project.ditherEvery)
            {
                DynamicSequencer.logger.Information($"Dither: exposure count {exposureCount}/{project.ditherEvery}, skipped");
                return Task.CompletedTask;
            }

            DynamicSequencer.logger.Information("Dither: dither command");

            DynamicSequencer.ditherLog.Clear();
            return _guiderMediator.Dither(token);
        }

        public virtual bool Validate()
        {
            List<string> i = new List<string>();
            if (!_guiderMediator.GetInfo().Connected)
            {
                i.Add("Guider not connected");
            }
            Issues = i;
            return i.Count == 0;
        }

        public override TimeSpan GetEstimatedDuration()
        {
            return TimeSpan.FromSeconds(_profileService.ActiveProfile.GuiderSettings.SettleTimeout);
        }
    }
}
