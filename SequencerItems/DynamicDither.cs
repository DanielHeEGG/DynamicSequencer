using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Dither")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "DitherSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Guider")]
    [Export(typeof(ISequenceItem))]
    public class DynamicDither : SequenceItem
    {
        private IProfileService _profileService;
        private IGuiderMediator _guiderMediator;

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
            var planner = new Planner();
            planner.Filter(_profileService);
            var project = planner.Best();
            if (project == null)
            {
                Notification.ShowWarning("Skipping DynamicDither - No valid project");
                throw new SequenceItemSkippedException("Skipping DynamicDither - No valid project");
            }
            var target = project.Best();
            if (target == null)
            {
                Notification.ShowWarning("Skipping DynamicDither - No valid target");
                throw new SequenceItemSkippedException("Skipping DynamicDither - No valid target");
            }
            var exposure = target.Best();
            if (exposure == null)
            {
                Notification.ShowWarning("Skipping DynamicDither - No valid exposure");
                throw new SequenceItemSkippedException("Skipping DynamicDither - No valid exposure");
            }

            if (DynamicSequencer.ditherLog.ContainsKey(exposure.ToString()) && DynamicSequencer.ditherLog[exposure.ToString()] >= project.ditherEvery)
            {
                DynamicSequencer.ditherLog.Clear();
                return _guiderMediator.Dither(token);
            }
            return Task.CompletedTask;
        }

        public override TimeSpan GetEstimatedDuration()
        {
            return TimeSpan.FromSeconds(_profileService.ActiveProfile.GuiderSettings.SettleTimeout);
        }
    }
}
