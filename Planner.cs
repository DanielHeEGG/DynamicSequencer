using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;

namespace DanielHeEGG.NINA.DynamicSequencer
{
    public class Planner
    {
        public List<PProject> _projects { get; }

        public Planner()
        {
            _projects = new List<PProject>();

            string projectDir = Path.Combine(CoreUtil.APPLICATIONTEMPPATH, "DynamicSequencerProjects");
            foreach (string filename in Directory.GetFiles(projectDir, "*.json"))
            {
                using (StreamReader r = File.OpenText(filename))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    PProject project = (PProject)serializer.Deserialize(r, typeof(PProject));
                    project.filename = filename;
                    _projects.Add(project);
                }
            }
        }

        public void WriteFiles()
        {
            foreach (PProject project in _projects)
            {
                using (StreamWriter w = new StreamWriter(project.filename))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(w, project);
                }
            }
        }

        public void Filter(IProfileService profileService)
        {
            DateTime time = DateTime.Now;

            ObserverInfo location = new ObserverInfo();
            location.Latitude = profileService.ActiveProfile.AstrometrySettings.Latitude;
            location.Longitude = profileService.ActiveProfile.AstrometrySettings.Longitude;
            location.Elevation = profileService.ActiveProfile.AstrometrySettings.Elevation;

            foreach (PProject project in _projects)
            {
                if (project.completion >= 1.0f)
                {
                    project.active = false;
                }

                if (!project.active)
                {
                    project.valid = false;
                    continue;
                }

                project.Filter(profileService, time, location);
            }
        }

        public PProject Best()
        {
            if (_projects.Count == 0)
            {
                return null;
            }

            if (DynamicSequencer.previousProject != null && _projects.Contains(DynamicSequencer.previousProject) && DynamicSequencer.previousProject.valid)
            {
                return DynamicSequencer.previousProject;
            }

            _projects.Sort(delegate (PProject x, PProject y)
            {
                int prioValid = Convert.ToInt32(y.valid) - Convert.ToInt32(x.valid);
                return prioValid == 0 ? (x.priority == y.priority ? (int)((y.completion - x.completion) * 1000) : x.priority - y.priority) : prioValid;
            });

            return _projects[0].valid ? _projects[0] : null;
        }
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class PProject
    {
        public string name { get; set; }
        public bool active { get; set; }
        public int priority { get; set; }
        public double minimumAltitude { get; set; }
        public double horizonOffset { get; set; }
        public bool balanceTargets { get; set; }
        public List<PTarget> targets { get; set; }

        [JsonIgnore]
        public string filename { get; set; }

        [JsonIgnore]
        public bool valid
        {
            get
            {
                foreach (PTarget target in targets)
                {
                    if (target.valid)
                    {
                        return true;
                    }
                }
                return false;
            }
            set
            {
                if (!value)
                {
                    foreach (PTarget target in targets)
                    {
                        target.valid = false;
                    }
                }
            }
        }

        [JsonIgnore]
        public int requiredAmount
        {
            get
            {
                int sum = 0;
                foreach (PTarget target in targets)
                {
                    sum += target.requiredAmount;
                }
                return sum;
            }
        }

        [JsonIgnore]
        public int acceptedAmount
        {
            get
            {
                int sum = 0;
                foreach (PTarget target in targets)
                {
                    sum += target.acceptedAmount;
                }
                return sum;
            }
        }

        [JsonIgnore]
        public double completion
        {
            get { return requiredAmount == 0 ? 1 : (double)acceptedAmount / requiredAmount; }
        }

        public void Filter(IProfileService profileService, DateTime time, ObserverInfo location)
        {
            foreach (PTarget target in targets)
            {
                double targetAltitude = AstrometryUtils.GetAltitude(location, target.rightAscension, target.declination, time);
                double targetAzimuth = AstrometryUtils.GetAzimuth(location, target.rightAscension, target.declination, time);

                if (targetAltitude < minimumAltitude)
                {
                    target.valid = false;
                    continue;
                }

                CustomHorizon horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;
                if (horizon != null && targetAltitude < horizon.GetAltitude(targetAzimuth) + horizonOffset)
                {
                    target.valid = false;
                    continue;
                }

                target.Filter(profileService, time, location);
            }
        }

        public PTarget Best()
        {
            if (targets.Count == 0)
            {
                return null;
            }

            if (DynamicSequencer.previousTarget != null && targets.Contains(DynamicSequencer.previousTarget) && DynamicSequencer.previousTarget.valid)
            {
                return DynamicSequencer.previousTarget;
            }

            targets.Sort(delegate (PTarget x, PTarget y)
            {
                int prioValid = Convert.ToInt32(y.valid) - Convert.ToInt32(x.valid);
                return prioValid == 0 ? (balanceTargets ? (int)((x.completion - y.completion) * 1000) : (int)((y.completion - x.completion) * 1000)) : prioValid;
            });

            return targets[0].valid ? targets[0] : null;
        }
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class PTarget
    {
        public string name { get; set; }
        public double rightAscension { get; set; }
        public double declination { get; set; }
        public double rotation { get; set; }
        public bool balanceFilters { get; set; }
        public List<PExposure> exposures { get; set; }

        [JsonIgnore]
        public bool valid
        {
            get
            {
                foreach (PExposure exposure in exposures)
                {
                    if (exposure.valid)
                    {
                        return true;
                    }
                }
                return false;
            }
            set
            {
                if (!value)
                {
                    foreach (PExposure exposure in exposures)
                    {
                        exposure.valid = false;
                    }
                }
            }
        }

        [JsonIgnore]
        public Coordinates coordinates
        {
            get { return new Coordinates(rightAscension, declination, Epoch.JNOW, Coordinates.RAType.Degrees); }
        }

        [JsonIgnore]
        public int requiredAmount
        {
            get
            {
                int sum = 0;
                foreach (PExposure exposure in exposures)
                {
                    sum += exposure.requiredAmount;
                }
                return sum;
            }
        }

        [JsonIgnore]
        public int acceptedAmount
        {
            get
            {
                int sum = 0;
                foreach (PExposure exposure in exposures)
                {
                    sum += exposure.acceptedAmount;
                }
                return sum;
            }
        }

        [JsonIgnore]
        public double completion
        {
            get { return requiredAmount == 0 ? 1 : (double)acceptedAmount / requiredAmount; }
        }

        public void Filter(IProfileService profileService, DateTime time, ObserverInfo location)
        {
            foreach (PExposure exposure in exposures)
            {
                if (AstrometryUtils.GetMoonSeparation(location, rightAscension, declination, time) < AstrometryUtils.GetMoonAvoidanceLorentzianSeparation(time, exposure.moonSeparationAngle, exposure.moonSeparationWidth) || exposure.completion >= 1.0)
                {
                    exposure.valid = false;
                    continue;
                }
                exposure.valid = true;
            }
        }

        public PExposure Best()
        {
            if (exposures.Count == 0)
            {
                return null;
            }

            exposures.Sort(delegate (PExposure x, PExposure y)
            {
                int prioValid = Convert.ToInt32(y.valid) - Convert.ToInt32(x.valid);
                double prioMoonSep = y.moonSeparationAngle * y.moonSeparationWidth - x.moonSeparationAngle * x.moonSeparationWidth;
                double prioProgress = balanceFilters ? x.completion - y.completion : y.completion - x.completion;

                return prioValid == 0 ? (prioMoonSep == 0 ? (int)(prioProgress * 1000) : (int)(prioMoonSep * 1000)) : prioValid;
            });

            return exposures[0].valid ? exposures[0] : null;
        }
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class PExposure
    {
        public string filter { get; set; }
        public double exposureTime { get; set; }
        public int gain { get; set; }
        public int offset { get; set; }
        public int binning { get; set; }
        public double moonSeparationAngle { get; set; }
        public int moonSeparationWidth { get; set; }
        public int requiredAmount { get; set; }
        public int acceptedAmount { get; set; }

        [JsonIgnore]
        public bool valid { get; set; }

        [JsonIgnore]
        public BinningMode binningMode
        {
            get { return new BinningMode((short)binning, (short)binning); }
        }

        [JsonIgnore]
        public double completion
        {
            get { return requiredAmount == 0 ? 1 : (double)acceptedAmount / requiredAmount; }
        }
    }
}
