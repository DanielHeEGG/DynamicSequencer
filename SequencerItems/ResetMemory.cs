using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Reset Memory")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "LoopSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Utility")]
    [Export(typeof(ISequenceItem))]
    public class ResetMemory : SequenceItem
    {
        public ResetMemory() { }

        private ResetMemory(ResetMemory cloneMe) : this()
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new ResetMemory(this);
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            DynamicSequencer.previousProject = "";
            DynamicSequencer.previousTarget = "";
            return Task.CompletedTask;
        }

    }
}
