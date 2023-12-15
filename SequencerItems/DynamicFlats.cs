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
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces.Mediator;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Take Trained Flats")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "BrainBulbSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_FlatDevice")]
    [Export(typeof(ISequenceItem))]
    public class DynamicFlats : SequenceItem, IValidatable
    {
        private IProfileService _profileService;
        private ICameraMediator _cameraMediator;
        private IImagingMediator _imagingMediator;
        private IImageSaveMediator _imageSaveMediator;
        private IRotatorMediator _rotatorMediator;
        private IFilterWheelMediator _filterWheelMediator;
        private IFlatDeviceMediator _flatDeviceMediator;

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
        public DynamicFlats(
            IProfileService profileService,
            ICameraMediator cameraMediator,
            IImagingMediator imagingMediator,
            IImageSaveMediator imageSaveMediator,
            IRotatorMediator rotatorMediator,
            IFilterWheelMediator filterWheelMediator,
            IFlatDeviceMediator flatDeviceMediator)
        {
            _profileService = profileService;
            _cameraMediator = cameraMediator;
            _imagingMediator = imagingMediator;
            _imageSaveMediator = imageSaveMediator;
            _rotatorMediator = rotatorMediator;
            _filterWheelMediator = filterWheelMediator;
            _flatDeviceMediator = flatDeviceMediator;
        }

        private DynamicFlats(DynamicFlats cloneMe) : this(
            cloneMe._profileService,
            cloneMe._cameraMediator,
            cloneMe._imagingMediator,
            cloneMe._imageSaveMediator,
            cloneMe._rotatorMediator,
            cloneMe._filterWheelMediator,
            cloneMe._flatDeviceMediator)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new DynamicFlats(this);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            DynamicSequencer.logger.Debug("Flat: execute");

            await _flatDeviceMediator.ToggleLight(true, progress, token);

            var planner = new Planner();
            foreach (PProject project in planner._projects)
            {
                if (!project.takeFlats || project.flatAmount <= 0 || !project.useMechanicalRotation) continue;

                foreach (PTarget target in project.targets)
                {
                    if (target.mechanicalRotation < 0)
                    {
                        DynamicSequencer.logger.Warning($"Flat: '{project.name}' - '{target.name}' does not contain rotation info, skipped");

                        continue;
                    }

                    foreach (PExposure exposure in target.exposures)
                    {
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
                            DynamicSequencer.logger.Error($"Flat: no matching filter for name '{exposure.filter}', skipped");

                            Notification.ShowWarning($"No matching filter name for {exposure.filter}");
                            continue;
                        }
                        await _filterWheelMediator.ChangeFilter(filter, token, progress);

                        var brightnessInfo = _profileService.ActiveProfile.FlatDeviceSettings.GetTrainedFlatExposureSetting(filter.Position, exposure.binningMode, exposure.gain, exposure.offset);
                        if (brightnessInfo == null)
                        {
                            DynamicSequencer.logger.Error($"Flat: no trained flat exposure for filter '{exposure.filter}', binning {exposure.binning}, gain {exposure.gain}, skipped");

                            Notification.ShowWarning($"No trained flat exposure for filter {exposure.filter}, binning {exposure.binning}, gain {exposure.gain}");
                            continue;
                        }
                        await _flatDeviceMediator.SetBrightness(brightnessInfo.Brightness, progress, token);

                        if (Math.Abs((double)_rotatorMediator.GetInfo().MechanicalPosition - target.mechanicalRotation) > 0.1)
                        {
                            DynamicSequencer.logger.Debug($"Flat: rotate to {target.mechanicalRotation}");

                            await _rotatorMediator.MoveMechanical((float)target.mechanicalRotation, token);
                        }

                        for (int i = 0; i < project.flatAmount; i++)
                        {
                            DynamicSequencer.logger.Debug($"Flat: '{project.name}' - '{target.name}' - '{exposure.filter}' progress {i + 1}/{project.flatAmount}");

                            var capture = new CaptureSequence()
                            {
                                ExposureTime = brightnessInfo.Time,
                                Binning = exposure.binningMode,
                                Gain = exposure.gain,
                                Offset = exposure.offset,
                                ImageType = CaptureSequence.ImageTypes.FLAT,
                                ProgressExposureCount = i,
                                TotalExposureCount = project.flatAmount
                            };

                            var exposureData = await _imagingMediator.CaptureImage(capture, token, progress);

                            var imageData = await exposureData.ToImageData(progress, token);

                            var prepareTask = _imagingMediator.PrepareImage(imageData, new PrepareImageParameters(null, false), token);

                            imageData.MetaData.Target.Name = target.name;
                            imageData.MetaData.Target.Coordinates = target.coordinates;
                            imageData.MetaData.Target.PositionAngle = target.skyRotation;
                            imageData.MetaData.Sequence.Title = project.name;

                            await _imageSaveMediator.Enqueue(imageData, prepareTask, progress, token);
                        }

                        DynamicSequencer.logger.Information($"Flat: '{project.name}' - '{target.name}' - '{exposure.filter}', {project.flatAmount} frames");
                    }
                }
                project.takeFlats = false;
            }
            await _flatDeviceMediator.ToggleLight(false, progress, token);
            planner.WriteFiles();
        }

        public virtual bool Validate()
        {
            List<string> i = new List<string>();
            if (!_cameraMediator.GetInfo().Connected)
            {
                i.Add("Camera not connected");
            }
            if (!_rotatorMediator.GetInfo().Connected)
            {
                i.Add("Rotator not connected");
            }
            if (!_filterWheelMediator.GetInfo().Connected)
            {
                i.Add("Filter wheel not connected");
            }
            if (!_flatDeviceMediator.GetInfo().Connected)
            {
                i.Add("Flat panel not connected");
            }
            Issues = i;
            return i.Count == 0;
        }
    }
}
