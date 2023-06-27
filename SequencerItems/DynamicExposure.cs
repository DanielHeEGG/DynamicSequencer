using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Take Exposure")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "CameraSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Camera")]
    [Export(typeof(ISequenceItem))]
    public class DynamicExposure : SequenceItem
    {
        private IProfileService _profileService;
        private ICameraMediator _cameraMediator;
        private IImagingMediator _imagingMediator;
        private IImageSaveMediator _imageSaveMediator;
        private IImageHistoryVM _imageHistoryVM;

        [ImportingConstructor]
        public DynamicExposure(
            IProfileService profileService,
            ICameraMediator cameraMediator,
            IImagingMediator imagingMediator,
            IImageSaveMediator imageSaveMediator,
            IImageHistoryVM imageHistoryVM)
        {
            _profileService = profileService;
            _cameraMediator = cameraMediator;
            _imagingMediator = imagingMediator;
            _imageSaveMediator = imageSaveMediator;
            _imageHistoryVM = imageHistoryVM;
        }

        private DynamicExposure(DynamicExposure cloneMe) : this(
            cloneMe._profileService,
            cloneMe._cameraMediator,
            cloneMe._imagingMediator,
            cloneMe._imageSaveMediator,
            cloneMe._imageHistoryVM)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new DynamicExposure(this);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            var planner = new Planner();
            planner.Filter(_profileService);
            var project = planner.Best();
            if (project == null)
            {
                Notification.ShowWarning("Skipping DynamicExposure - No valid project");
                throw new SequenceItemSkippedException("Skipping DynamicExposure - No valid project");
            }
            var target = project.Best();
            if (target == null)
            {
                Notification.ShowWarning("Skipping DynamicExposure - No valid target");
                throw new SequenceItemSkippedException("Skipping DynamicExposure - No valid target");
            }
            var exposure = target.Best();
            if (exposure == null)
            {
                Notification.ShowWarning("Skipping DynamicExposure - No valid exposure");
                throw new SequenceItemSkippedException("Skipping DynamicExposure - No valid exposure");
            }

            var capture = new CaptureSequence()
            {
                ExposureTime = exposure.exposureTime,
                Binning = exposure.binningMode,
                Gain = exposure.gain,
                Offset = exposure.offset,
                ImageType = CaptureSequence.ImageTypes.LIGHT,
                ProgressExposureCount = 0,
                TotalExposureCount = 1
            };

            var imageParams = new PrepareImageParameters(true, true);

            var exposureData = await _imagingMediator.CaptureImage(capture, token, progress);

            var imageData = await exposureData.ToImageData(progress, token);

            var prepareTask = _imagingMediator.PrepareImage(imageData, imageParams, token);

            imageData.MetaData.Target.Name = target.name;
            imageData.MetaData.Target.Coordinates = target.coordinates;
            imageData.MetaData.Target.Rotation = target.rotation;
            imageData.MetaData.Sequence.Title = project.name;

            await _imageSaveMediator.Enqueue(imageData, prepareTask, progress, token);

            var imageStats = await imageData.Statistics;

            _imageHistoryVM.Add(imageData.MetaData.Image.Id, imageStats, CaptureSequence.ImageTypes.LIGHT);

            exposure.acceptedAmount++;
            planner.WriteFiles();
        }
    }
}
