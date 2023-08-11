using System.ComponentModel.Composition;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Loop While Project Available")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "LoopSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Condition")]
    [Export(typeof(ISequenceCondition))]
    public class ProjectAvailableCondition : SequenceCondition
    {
        public IProfileService _profileService;

        [ImportingConstructor]
        public ProjectAvailableCondition(IProfileService profileService)
        {
            _profileService = profileService;
        }
        private ProjectAvailableCondition(ProjectAvailableCondition cloneMe) : this(cloneMe._profileService)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new ProjectAvailableCondition(this);
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            Planner planner = new Planner();
            planner.Filter(_profileService);
            foreach (PProject project in planner._projects)
            {
                if (project.active) return true;
            }
            return false;
        }
    }
}
