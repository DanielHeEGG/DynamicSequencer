﻿using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Wait Until Target Available")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "WaitForAltitudeSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Utility")]
    [Export(typeof(ISequenceItem))]
    public class WaitForTargetAvailable : SequenceItem
    {
        IProfileService _profileService;

        [ImportingConstructor]
        public WaitForTargetAvailable(IProfileService profileService)
        {
            _profileService = profileService;
        }

        private WaitForTargetAvailable(WaitForTargetAvailable cloneMe) : this(cloneMe._profileService)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new WaitForTargetAvailable(this);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            DynamicSequencer.logger.Information("WaitForTargetAvailable: start");

            while (true)
            {
                Planner planner = new Planner();
                planner.Filter(_profileService);
                if (planner.Best(_profileService) != null)
                {
                    break;
                }
                await CoreUtil.Delay(TimeSpan.FromSeconds(1), token);
            }

            DynamicSequencer.logger.Information("WaitForTargetAvailable: complete");
        }
    }
}
