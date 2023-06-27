using System.ComponentModel.Composition;

using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Loop While Target Available")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "WaitForAltitudeSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Condition")]
    [Export(typeof(ISequenceCondition))]
    public class TargetAvailableCondition : SequenceCondition
    {
        public IProfileService _profileService;

        [ImportingConstructor]
        public TargetAvailableCondition(IProfileService profileSerivce)
        {
            _profileService = profileSerivce;
        }
        private TargetAvailableCondition(TargetAvailableCondition cloneMe) : this(cloneMe._profileService)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new TargetAvailableCondition(this);
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            Planner planner = new Planner();
            planner.Filter(_profileService);
            return planner.Best() != null;
        }
    }
}
