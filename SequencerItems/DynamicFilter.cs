using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Switch Filter")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "FW_NoFill_SVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_FilterWheel")]
    [Export(typeof(ISequenceItem))]
    public class DynamicFilter : SequenceItem, IValidatable
    {
        private IProfileService _profileService;
        private IFilterWheelMediator _filterWheelMediator;

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
        public DynamicFilter(IProfileService profileService, IFilterWheelMediator filterWheelMediator)
        {
            _profileService = profileService;
            _filterWheelMediator = filterWheelMediator;
        }

        private DynamicFilter(DynamicFilter cloneMe) : this(cloneMe._profileService, cloneMe._filterWheelMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new DynamicFilter(this);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            DynamicSequencer.logger.Debug("Filter: execute");

            var planner = new Planner();
            planner.Filter(_profileService);
            var project = planner.GetProjectFromString(DynamicSequencer.currentProject);
            if (project == null || !project.valid)
            {
                DynamicSequencer.logger.Information("Filter: current project not valid, skipped");
                return;
            }
            var target = project.getTargetFromString(DynamicSequencer.currentTarget);
            if (target == null || !target.valid)
            {
                DynamicSequencer.logger.Information("Filter: current target not valid, skipped");
                return;
            }
            var exposure = target.Best();
            if (exposure == null)
            {
                DynamicSequencer.logger.Information("Filter: no valid exposure, skipped");
                return;
            }

            FilterInfo filter = null;
            foreach (FilterInfo filterInfo in _profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters)
            {
                if (filterInfo.Name == exposure.filter)
                {
                    filter = filterInfo;
                    break;
                }
            }
            if (filter == null)
            {
                DynamicSequencer.logger.Error($"Filter: no matching filter for name '{exposure.filter}'");

                Notification.ShowWarning("Skipping DynamicFilter - No matching filter");
                throw new SequenceItemSkippedException("Skipping DynamicFilter - No matching filter");
            }

            DynamicSequencer.logger.Information($"Filter: selected '{exposure.filter}'");

            await _filterWheelMediator.ChangeFilter(filter, token, progress);
        }

        public virtual bool Validate()
        {
            List<string> i = new List<string>();
            if (!_filterWheelMediator.GetInfo().Connected)
            {
                i.Add("Filter wheel not connected");
            }
            Issues = i;
            return i.Count == 0;
        }
    }
}
