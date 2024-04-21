using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
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
        public double ExposureTime { get; set; }
        public int Gain { get; set; }
        public int Offset { get; set; }
        public string ImageType { get; set; }
        public BinningMode Binning { get; set; }

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

            // Autofocus trigger checks for this
            ImageType = "LIGHT";
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
            DynamicSequencer.logger.Debug("Exposure: execute");

            var planner = new Planner();
            planner.Filter(_profileService);
            var project = planner.GetProjectFromString(DynamicSequencer.currentProject);
            if (project == null || !project.valid)
            {
                DynamicSequencer.logger.Information("Exposure: current project not valid, skipped");
                return;
            }
            var target = project.getTargetFromString(DynamicSequencer.currentTarget);
            if (target == null || !target.valid)
            {
                DynamicSequencer.logger.Information("Exposure: current target not valid, skipped");
                return;
            }
            var exposure = target.Best(_profileService);
            if (exposure == null)
            {
                DynamicSequencer.logger.Information("Exposure: no valid exposure, skipped");
                return;
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

            DynamicSequencer.logger.Information($"Exposure: taking exposure '{project.name}' - '{target.name}' - '{exposure.filter}'");

            var exposureData = await _imagingMediator.CaptureImage(capture, token, progress);

            var imageData = await exposureData.ToImageData(progress, token);

            var renderedImage = await _imagingMediator.PrepareImage(imageData, new PrepareImageParameters(true, true), token);

            imageData.MetaData.Target.Name = target.name;
            imageData.MetaData.Target.Coordinates = target.coordinates;
            imageData.MetaData.Target.PositionAngle = target.skyRotation;
            imageData.MetaData.Sequence.Title = project.name;

            if (project.imageGrader.GradeImage(renderedImage.RawImageData))
            {
                exposure.acceptedAmount++;

                DynamicSequencer.logger.Information($"Exposure: image accepted, progress {exposure.acceptedAmount}/{exposure.requiredAmount}");

                // add to ditherLog
                if (DynamicSequencer.ditherLog.ContainsKey(exposure.ToString()))
                {
                    DynamicSequencer.logger.Debug("Exposure: in ditherLog, increment count");

                    DynamicSequencer.ditherLog[exposure.ToString()]++;
                }
                else
                {
                    DynamicSequencer.logger.Debug("Exposure: not in ditherLog, adding");

                    DynamicSequencer.ditherLog.Add(exposure.ToString(), 1);
                }

                // NOTE: for flatLog entries, project.useMechanicalRotation and target.mechanicalRotation conditions are checked in DynamicFlats, not here
                if (project.flatType == FlatType.NIGHTLY)
                {
                    // add exposure to filterLog
                    if (!DynamicSequencer.flatLog.ContainsKey(project.ToString()))
                    {
                        DynamicSequencer.logger.Debug($"Exposure: project '{project.name}' not in flatLog, adding");
                        DynamicSequencer.flatLog.Add(project.ToString(), new Dictionary<string, List<string>>());
                    }
                    if (!DynamicSequencer.flatLog[project.ToString()].ContainsKey(target.ToString()))
                    {
                        DynamicSequencer.logger.Debug($"Exposure: ---- target '{target.name}' not in flatLog, adding");
                        DynamicSequencer.flatLog[project.ToString()].Add(target.ToString(), new List<string>());
                    }
                    if (!DynamicSequencer.flatLog[project.ToString()][target.ToString()].Contains(exposure.ToString()))
                    {
                        DynamicSequencer.logger.Debug($"Exposure: ---- ---- exposure '{exposure.filter}' not in flatLog, adding");
                        DynamicSequencer.flatLog[project.ToString()][target.ToString()].Add(exposure.ToString());
                    }
                }
                if (target.completion >= 1.0f)
                {
                    DynamicSequencer.logger.Debug("Exposure: target complete");

                    if (project.flatType == FlatType.UPON_TARGET_COMPLETION)
                    {
                        // add entire target to flatLog
                        if (!DynamicSequencer.flatLog.ContainsKey(project.ToString()))
                        {
                            DynamicSequencer.logger.Debug($"Exposure: project '{project.name}' not in flatLog, adding");
                            DynamicSequencer.flatLog.Add(project.ToString(), new Dictionary<string, List<string>>());
                        }
                        if (!DynamicSequencer.flatLog[project.ToString()].ContainsKey(target.ToString()))
                        {
                            DynamicSequencer.logger.Debug($"Exposure: ---- target '{target.name}' not in flatLog, adding");
                            DynamicSequencer.flatLog[project.ToString()].Add(target.ToString(), new List<string>());
                        }
                        foreach (PExposure flatExposure in target.exposures)
                        {
                            if (!DynamicSequencer.flatLog[project.ToString()][target.ToString()].Contains(flatExposure.ToString()))
                            {
                                DynamicSequencer.logger.Debug($"Exposure: ---- ---- exposure '{flatExposure.filter}' not in flatLog, adding");
                                DynamicSequencer.flatLog[project.ToString()][target.ToString()].Add(flatExposure.ToString());
                            }
                        }
                    }
                }
                if (project.completion >= 1.0f)
                {
                    DynamicSequencer.logger.Debug("Exposure: project complete");

                    project.active = false;

                    if (project.flatType == FlatType.UPON_PROJECT_COMPLETION)
                    {
                        // add entire project to flatLog
                        if (!DynamicSequencer.flatLog.ContainsKey(project.ToString()))
                        {
                            DynamicSequencer.logger.Debug($"Exposure: project '{project.name}' not in flatLog, adding");
                            DynamicSequencer.flatLog.Add(project.ToString(), new Dictionary<string, List<string>>());
                        }
                        foreach (PTarget flatTarget in project.targets)
                        {
                            if (!DynamicSequencer.flatLog[project.ToString()].ContainsKey(flatTarget.ToString()))
                            {
                                DynamicSequencer.logger.Debug($"Exposure: ---- target '{flatTarget.name}' not in flatLog, adding");
                                DynamicSequencer.flatLog[project.ToString()].Add(flatTarget.ToString(), new List<string>());
                            }
                            foreach (PExposure flatExposure in flatTarget.exposures)
                            {
                                if (!DynamicSequencer.flatLog[project.ToString()][flatTarget.ToString()].Contains(flatExposure.ToString()))
                                {
                                    DynamicSequencer.logger.Debug($"Exposure: ---- ---- exposure '{flatExposure.filter}' not in flatLog, adding");
                                    DynamicSequencer.flatLog[project.ToString()][flatTarget.ToString()].Add(flatExposure.ToString());
                                }
                            }
                        }
                    }
                }
                planner.WriteFiles();
            }
            else
            {
                DynamicSequencer.logger.Information("Exposure: image rejected");

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
            var project = planner.GetProjectFromString(DynamicSequencer.currentProject);
            if (project == null || !project.valid) return TimeSpan.FromSeconds(1);
            var target = project.getTargetFromString(DynamicSequencer.currentTarget);
            if (target == null || !target.valid) return TimeSpan.FromSeconds(1);
            var exposure = target.Best(_profileService);
            if (exposure == null) return TimeSpan.FromSeconds(1);

            return TimeSpan.FromSeconds(exposure.exposureTime);
        }
    }
}
