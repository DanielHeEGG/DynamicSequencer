using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Take Exposure")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "CameraSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Camera")]
    [Export(typeof(ISequenceItem))]
    public class DynamicExposure : SequenceItem, IExposureItem, IValidatable
    {
        private IProfileService _profileService;
        private ICameraMediator _cameraMediator;
        private IImagingMediator _imagingMediator;
        private IImageSaveMediator _imageSaveMediator;
        private IImageHistoryVM _imageHistoryVM;

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

        // To satisfy IExposureItem interface requirements, DO NOT USE
        public double ExposureTime { get; }
        public int Gain { get; }
        public int Offset { get; }
        public string ImageType { get; }
        public BinningMode Binning { get; }

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

            var exposureData = await _imagingMediator.CaptureImage(capture, token, progress);

            var imageData = await exposureData.ToImageData(progress, token);

            var renderedImage = await _imagingMediator.PrepareImage(imageData, new PrepareImageParameters(true, true), token);

            imageData.MetaData.Target.Name = target.name;
            imageData.MetaData.Target.Coordinates = target.coordinates;
            imageData.MetaData.Target.Rotation = target.skyRotation;
            imageData.MetaData.Sequence.Title = project.name;

            if (project.imageGrader.GradeImage(renderedImage.RawImageData))
            {
                exposure.acceptedAmount++;
                if (project.completion >= 1.0f)
                {
                    project.active = false;
                    project.takeFlats = true;
                }
                planner.WriteFiles();

                if (DynamicSequencer.ditherLog.ContainsKey(exposure.ToString())) DynamicSequencer.ditherLog[exposure.ToString()]++;
                else DynamicSequencer.ditherLog.Add(exposure.ToString(), 1);
            }
            else
            {
                imageData.MetaData.Sequence.Title += " - REJECTED";
            }

            await _imageSaveMediator.Enqueue(imageData, Task.FromResult(renderedImage), progress, token);

            var imageStats = await imageData.Statistics;

            _imageHistoryVM.Add(imageData.MetaData.Image.Id, imageStats, CaptureSequence.ImageTypes.LIGHT);
        }

        public virtual bool Validate()
        {
            List<string> i = new List<string>();
            if (!_cameraMediator.GetInfo().Connected)
            {
                i.Add("Camera not connected");
            }
            Issues = i;
            return i.Count == 0;
        }

        public override TimeSpan GetEstimatedDuration()
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

            return TimeSpan.FromSeconds(exposure.exposureTime);
        }
    }
}
