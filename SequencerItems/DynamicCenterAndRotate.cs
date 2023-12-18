using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using DanielHeEGG.NINA.DynamicSequencer.PlannerEngine;

using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.PlateSolving;
using NINA.PlateSolving.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.ViewModel;

namespace DanielHeEGG.NINA.DynamicSequencer.SequencerItems
{
    [ExportMetadata("Name", "DS: Center and Rotate")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "PlatesolveAndRotateSVG")]
    [ExportMetadata("Category", "Lbl_SequenceCategory_Telescope")]
    [Export(typeof(ISequenceItem))]
    public class DynamicCenterAndRotate : SequenceItem, IValidatable
    {
        private IProfileService _profileService;
        private ITelescopeMediator _telescopeMediator;
        private IImagingMediator _imagingMediator;
        private IRotatorMediator _rotatorMediator;
        private IFilterWheelMediator _filterWheelMediator;
        private IGuiderMediator _guiderMediator;
        private IDomeMediator _domeMediator;
        private IDomeFollower _domeFollower;
        private IPlateSolverFactory _plateSolverFactory;
        private IWindowServiceFactory _windowServiceFactory;

        public PlateSolvingStatusVM _plateSolveStatusVM { get; } = new PlateSolvingStatusVM();

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
        public DynamicCenterAndRotate(
            IProfileService profileService,
            ITelescopeMediator telescopeMediator,
            IImagingMediator imagingMediator,
            IRotatorMediator rotatorMediator,
            IFilterWheelMediator filterWheelMediator,
            IGuiderMediator guiderMediator,
            IDomeMediator domeMediator,
            IDomeFollower domeFollower,
            IPlateSolverFactory plateSolverFactory,
            IWindowServiceFactory windowServiceFactory)
        {
            _profileService = profileService;
            _telescopeMediator = telescopeMediator;
            _imagingMediator = imagingMediator;
            _rotatorMediator = rotatorMediator;
            _filterWheelMediator = filterWheelMediator;
            _guiderMediator = guiderMediator;
            _domeMediator = domeMediator;
            _domeFollower = domeFollower;
            _plateSolverFactory = plateSolverFactory;
            _windowServiceFactory = windowServiceFactory;
        }

        private DynamicCenterAndRotate(DynamicCenterAndRotate cloneMe) : this(
            cloneMe._profileService,
            cloneMe._telescopeMediator,
            cloneMe._imagingMediator,
            cloneMe._rotatorMediator,
            cloneMe._filterWheelMediator,
            cloneMe._guiderMediator,
            cloneMe._domeMediator,
            cloneMe._domeFollower,
            cloneMe._plateSolverFactory,
            cloneMe._windowServiceFactory)
        {
            CopyMetaData(cloneMe);
        }

        public override object Clone()
        {
            return new DynamicCenterAndRotate(this);
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            DynamicSequencer.logger.Debug("CenterAndRotate: execute");

            var planner = new Planner();
            planner.Filter(_profileService);
            var project = planner.Best(_profileService);
            if (project == null)
            {
                DynamicSequencer.logger.Warning("CenterAndRotate: no project");

                Notification.ShowWarning("Skipping DynamicCenterAndRotate - No valid project");
                throw new SequenceItemSkippedException("Skipping DynamicCenterAndRotate - No valid project");
            }
            var target = project.Best(_profileService);
            if (target == null)
            {
                DynamicSequencer.logger.Warning("CenterAndRotate: no target");

                Notification.ShowWarning("Skipping DynamicCenterAndRotate - No valid target");
                throw new SequenceItemSkippedException("Skipping DynamicCenterAndRotate - No valid target");
            }

            if (project.ToString() == DynamicSequencer.currentProject && target.ToString() == DynamicSequencer.currentTarget)
            {
                DynamicSequencer.logger.Information("CenterAndRotate: same target, skipped");

                return;
            }

            DynamicSequencer.logger.Information($"CenterAndRotate: slew to new target '{project.name}' - '{target.name}'");

            if (_telescopeMediator.GetInfo().AtPark)
            {
                DynamicSequencer.logger.Error("CenterAndRotate: telescope parked, skipped");
                Notification.ShowError(Loc.Instance["LblTelescopeParkedWarning"]);
                throw new SequenceEntityFailedException(Loc.Instance["LblTelescopeParkedWarning"]);
            }
            var service = _windowServiceFactory.Create();
            progress = _plateSolveStatusVM.CreateLinkedProgress(progress);
            service.Show(_plateSolveStatusVM, Loc.Instance["Lbl_SequenceItem_Platesolving_CenterAndRotate_Name"], System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ToolWindow);

            bool stoppedGuiding = false;
            try
            {
                float rotationDistance = float.MaxValue;

                stoppedGuiding = await _guiderMediator.StopGuiding(token);
                progress?.Report(new ApplicationStatus() { Status = Loc.Instance["LblSlew"] });
                await _telescopeMediator.SlewToCoordinatesAsync(target.coordinates, token);

                var domeInfo = _domeMediator.GetInfo();
                if (domeInfo.Connected && domeInfo.CanSetAzimuth && !_domeFollower.IsFollowing)
                {
                    progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblSynchronizingDome"] });
                    Logger.Info($"Center and Rotate - Synchronize dome to scope since dome following is not enabled");
                    if (!await _domeFollower.TriggerTelescopeSync())
                    {
                        Notification.ShowWarning(Loc.Instance["LblDomeSyncFailureDuringCentering"]);
                        Logger.Warning("Center and Rotate - Synchronize dome operation didn't complete successfully. Moving on");
                    }
                }
                progress?.Report(new ApplicationStatus() { Status = string.Empty });

                if ((!project.useMechanicalRotation) || (target.mechanicalRotation < 0))
                {
                    DynamicSequencer.logger.Debug("CenterAndRotate: rotate with skyRotation");

                    var targetRotation = (float)target.skyRotation;

                    /* Loop until the rotation is within tolerances*/
                    do
                    {
                        var solveResult = await Solve(progress, token, target.coordinates);
                        if (!solveResult.Success)
                        {
                            throw new SequenceEntityFailedException(Loc.Instance["LblPlatesolveFailed"]);
                        }

                        var orientation = (float)solveResult.PositionAngle;
                        _rotatorMediator.Sync(orientation);

                        var prevTargetRotation = targetRotation;
                        targetRotation = _rotatorMediator.GetTargetPosition(prevTargetRotation);
                        if (Math.Abs(targetRotation - prevTargetRotation) > 0.1)
                        {
                            Logger.Info($"Rotator target position {target.skyRotation} adjusted to {targetRotation} to be within the allowed mechanical range");
                            Notification.ShowInformation(string.Format(Loc.Instance["LblRotatorRangeAdjusted"], targetRotation));
                        }

                        rotationDistance = targetRotation - orientation;
                        if (_profileService.ActiveProfile.RotatorSettings.RangeType == RotatorRangeTypeEnum.FULL)
                        {
                            // If the full rotation range is allowed, then consider the 180-degree rotated orientation as well in case it is closer
                            var movement = AstroUtil.EuclidianModulus(rotationDistance, 180);
                            var movement2 = movement - 180;

                            if (movement < Math.Abs(movement2))
                            {
                                rotationDistance = movement;
                            }
                            else
                            {
                                targetRotation = AstroUtil.EuclidianModulus(targetRotation + 180, 360);
                                Logger.Info($"Changing rotation target to {targetRotation} instead since it is closer to the current position");
                                rotationDistance = movement2;
                            }
                        }

                        if (!Angle.ByDegree(rotationDistance).Equals(Angle.Zero, Angle.ByDegree(_profileService.ActiveProfile.PlateSolveSettings.RotationTolerance), true))
                        {
                            Logger.Info($"Rotator not inside tolerance {_profileService.ActiveProfile.PlateSolveSettings.RotationTolerance} - Current {orientation}° / Target: {target.skyRotation}° - Moving rotator relatively by {rotationDistance}°");
                            progress?.Report(new ApplicationStatus() { Status = Loc.Instance["LblRotating"] });
                            await _rotatorMediator.MoveRelative(rotationDistance, token);
                            progress?.Report(new ApplicationStatus() { Status = string.Empty });
                            token.ThrowIfCancellationRequested();
                        }
                    } while (!Angle.ByDegree(rotationDistance).Equals(Angle.Zero, Angle.ByDegree(_profileService.ActiveProfile.PlateSolveSettings.RotationTolerance), true));

                    if (project.useMechanicalRotation)
                    {
                        DynamicSequencer.logger.Debug("CenterAndRotate: update mechanicalRotation");

                        target.mechanicalRotation = (double)_rotatorMediator.GetInfo().MechanicalPosition;
                        planner.WriteFiles();
                    }
                }
                if (project.useMechanicalRotation && Math.Abs((double)_rotatorMediator.GetInfo().MechanicalPosition - target.mechanicalRotation) > 0.1)
                {
                    DynamicSequencer.logger.Debug("CenterAndRotate: rotate with mechanicalRotation");

                    await _rotatorMediator.MoveMechanical((float)target.mechanicalRotation, token);
                }

                /* Once everything is in place do a centering of the object */
                if (project.centerTargets)
                {
                    DynamicSequencer.logger.Debug("CenterAndRotate: center target");

                    var centerResult = await DoCenter(progress, token, target.coordinates);

                    if (!centerResult.Success)
                    {
                        throw new Exception(Loc.Instance["LblPlatesolveFailed"]);
                    }
                }
            }
            finally
            {
                DynamicSequencer.logger.Information("CenterAndRotate: complete, set memory");

                DynamicSequencer.currentProject = project.ToString();
                DynamicSequencer.currentTarget = target.ToString();
                DynamicSequencer.ditherLog.Clear();

                if (stoppedGuiding)
                {
                    try
                    {
                        var restartedGuiding = await _guiderMediator.StartGuiding(false, progress, token);
                        if (!restartedGuiding)
                        {
                            Logger.Error("Failed to resume guiding after CenterAndRotate");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to resume guiding after CenterAndRotate", e);
                    }
                }

                service.DelayedClose(TimeSpan.FromSeconds(10));
            }
        }

        public virtual bool Validate()
        {
            List<string> i = new List<string>();
            if (!_telescopeMediator.GetInfo().Connected)
            {
                i.Add("Telescope not connected");
            }
            if (!_rotatorMediator.GetInfo().Connected)
            {
                i.Add("Rotator not connected");
            }
            Issues = i;
            return i.Count == 0;
        }

        private async Task<PlateSolveResult> Solve(IProgress<ApplicationStatus> progress, CancellationToken token, Coordinates coordinates)
        {
            var plateSolver = _plateSolverFactory.GetPlateSolver(_profileService.ActiveProfile.PlateSolveSettings);
            var blindSolver = _plateSolverFactory.GetBlindSolver(_profileService.ActiveProfile.PlateSolveSettings);

            var solver = _plateSolverFactory.GetCaptureSolver(plateSolver, blindSolver, _imagingMediator, _filterWheelMediator);
            var parameter = new CaptureSolverParameter()
            {
                Attempts = _profileService.ActiveProfile.PlateSolveSettings.NumberOfAttempts,
                Binning = _profileService.ActiveProfile.PlateSolveSettings.Binning,
                Coordinates = coordinates,
                DownSampleFactor = _profileService.ActiveProfile.PlateSolveSettings.DownSampleFactor,
                FocalLength = _profileService.ActiveProfile.TelescopeSettings.FocalLength,
                MaxObjects = _profileService.ActiveProfile.PlateSolveSettings.MaxObjects,
                PixelSize = _profileService.ActiveProfile.CameraSettings.PixelSize,
                ReattemptDelay = TimeSpan.FromMinutes(_profileService.ActiveProfile.PlateSolveSettings.ReattemptDelay),
                Regions = _profileService.ActiveProfile.PlateSolveSettings.Regions,
                SearchRadius = _profileService.ActiveProfile.PlateSolveSettings.SearchRadius,
                BlindFailoverEnabled = _profileService.ActiveProfile.PlateSolveSettings.BlindFailoverEnabled
            };

            var seq = new CaptureSequence(
                _profileService.ActiveProfile.PlateSolveSettings.ExposureTime,
                CaptureSequence.ImageTypes.SNAPSHOT,
                _profileService.ActiveProfile.PlateSolveSettings.Filter,
                new BinningMode(_profileService.ActiveProfile.PlateSolveSettings.Binning, _profileService.ActiveProfile.PlateSolveSettings.Binning),
                1
            );
            return await solver.Solve(seq, parameter, _plateSolveStatusVM.Progress, progress, token);
        }

        protected async Task<PlateSolveResult> DoCenter(IProgress<ApplicationStatus> progress, CancellationToken token, Coordinates coordinates)
        {
            if (_telescopeMediator.GetInfo().AtPark)
            {
                Notification.ShowError(Loc.Instance["LblTelescopeParkedWarning"]);
                throw new SequenceEntityFailedException(Loc.Instance["LblTelescopeParkedWarning"]);
            }
            progress?.Report(new ApplicationStatus() { Status = Loc.Instance["LblSlew"] });
            await _telescopeMediator.SlewToCoordinatesAsync(coordinates, token);

            var domeInfo = _domeMediator.GetInfo();
            if (domeInfo.Connected && domeInfo.CanSetAzimuth && !_domeFollower.IsFollowing)
            {
                progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblSynchronizingDome"] });
                Logger.Info($"Centering Solver - Synchronize dome to scope since dome following is not enabled");
                if (!await _domeFollower.TriggerTelescopeSync())
                {
                    Notification.ShowWarning(Loc.Instance["LblDomeSyncFailureDuringCentering"]);
                    Logger.Warning("Centering Solver - Synchronize dome operation didn't complete successfully. Moving on");
                }
            }
            progress?.Report(new ApplicationStatus() { Status = string.Empty });

            var plateSolver = _plateSolverFactory.GetPlateSolver(_profileService.ActiveProfile.PlateSolveSettings);
            var blindSolver = _plateSolverFactory.GetBlindSolver(_profileService.ActiveProfile.PlateSolveSettings);

            var solver = _plateSolverFactory.GetCenteringSolver(plateSolver, blindSolver, _imagingMediator, _telescopeMediator, _filterWheelMediator, _domeMediator, _domeFollower);
            var parameter = new CenterSolveParameter()
            {
                Attempts = _profileService.ActiveProfile.PlateSolveSettings.NumberOfAttempts,
                Binning = _profileService.ActiveProfile.PlateSolveSettings.Binning,
                Coordinates = coordinates,
                DownSampleFactor = _profileService.ActiveProfile.PlateSolveSettings.DownSampleFactor,
                FocalLength = _profileService.ActiveProfile.TelescopeSettings.FocalLength,
                MaxObjects = _profileService.ActiveProfile.PlateSolveSettings.MaxObjects,
                PixelSize = _profileService.ActiveProfile.CameraSettings.PixelSize,
                ReattemptDelay = TimeSpan.FromMinutes(_profileService.ActiveProfile.PlateSolveSettings.ReattemptDelay),
                Regions = _profileService.ActiveProfile.PlateSolveSettings.Regions,
                SearchRadius = _profileService.ActiveProfile.PlateSolveSettings.SearchRadius,
                Threshold = _profileService.ActiveProfile.PlateSolveSettings.Threshold,
                NoSync = _profileService.ActiveProfile.TelescopeSettings.NoSync,
                BlindFailoverEnabled = _profileService.ActiveProfile.PlateSolveSettings.BlindFailoverEnabled
            };

            var seq = new CaptureSequence(
                _profileService.ActiveProfile.PlateSolveSettings.ExposureTime,
                CaptureSequence.ImageTypes.SNAPSHOT,
                _profileService.ActiveProfile.PlateSolveSettings.Filter,
                new BinningMode(_profileService.ActiveProfile.PlateSolveSettings.Binning, _profileService.ActiveProfile.PlateSolveSettings.Binning),
                1
            );
            return await solver.Center(seq, parameter, _plateSolveStatusVM.Progress, progress, token);
        }
    }
}
