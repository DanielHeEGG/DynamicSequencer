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
