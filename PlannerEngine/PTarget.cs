using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using NINA.Astrometry;
using NINA.Profile.Interfaces;

namespace DanielHeEGG.NINA.DynamicSequencer.PlannerEngine
{
    [JsonObject(MemberSerialization.OptOut)]
    public class PTarget
    {
        public string name { get; set; } = "Default Target";
        public double rightAscension { get; set; } = 0;
        public double declination { get; set; } = 0;
        public double skyRotation { get; set; } = 0;
        public double mechanicalRotation { get; set; } = -1;
        public bool takeFlatsOverride { get; set; } = false;
        public List<PExposureSelectionPriority> exposureSelectionPriority { get; set; } = [PExposureSelectionPriority.SELECTIVITY, PExposureSelectionPriority.N_COMPLETION];
        public List<PExposure> exposures { get; set; } = [new PExposure()];

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
            get { return new Coordinates(rightAscension, declination, Epoch.J2000, Coordinates.RAType.Degrees); }
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
                DynamicSequencer.logger.Debug($"Planner: ---- ---- filtering exposure '{exposure.filter}'");

                if (exposure.completion >= 1.0f)
                {
                    exposure.valid = false;

                    DynamicSequencer.logger.Debug($"Planner: ---- ---- rejected (completed)");

                    continue;
                }

                if (AstrometryUtils.GetMoonSeparation(location, rightAscension, declination, time) < AstrometryUtils.GetMoonAvoidanceLorentzianSeparation(time, exposure.moonSeparationAngle, exposure.moonSeparationWidth) || exposure.completion >= 1.0)
                {
                    exposure.valid = false;

                    DynamicSequencer.logger.Debug($"Planner: ---- ---- rejected (moon separation)");

                    continue;
                }

                exposure.valid = true;
            }
        }

        public PExposure Best(IProfileService profileService)
        {
            if (exposures.Count == 0)
            {
                DynamicSequencer.logger.Debug($"Planner: ---- ---- no exposure selected (empty list)");

                return null;
            }

            // valid exposures placed by reference in separate list to preserve the order in "exposures"
            List<PExposure> validExposures = new List<PExposure>();
            foreach (PExposure exposure in exposures)
            {
                if (exposure.valid) validExposures.Add(exposure);
            }

            validExposures.Sort(delegate (PExposure x, PExposure y)
            {
                int prioCompletion = (int)((y.completion - x.completion) * 1000);
                int prioSelectivity = (int)(y.moonSeparationAngle * y.moonSeparationWidth - x.moonSeparationAngle * x.moonSeparationWidth);
                foreach (PExposureSelectionPriority item in exposureSelectionPriority)
                {
                    switch (item)
                    {
                        case PExposureSelectionPriority.COMPLETION:
                            if (prioCompletion != 0) return prioCompletion;
                            continue;
                        case PExposureSelectionPriority.N_COMPLETION:
                            if (prioCompletion != 0) return -prioCompletion;
                            continue;
                        case PExposureSelectionPriority.SELECTIVITY:
                            if (prioSelectivity != 0) return prioSelectivity;
                            continue;
                        case PExposureSelectionPriority.N_SELECTIVITY:
                            if (prioSelectivity != 0) return -prioSelectivity;
                            continue;
                        default:
                            return 0;
                    }
                }
                return 0;
            });

            if (validExposures.Count != 0 && validExposures[0].valid)
            {
                DynamicSequencer.logger.Debug($"Planner: ---- ---- exposure '{validExposures[0].filter}' selected (best exposure)");

                return validExposures[0];
            }

            DynamicSequencer.logger.Debug($"Planner: ---- ---- no exposure selected (no valid exposure)");

            return null;
        }

        public PExposure GetExposureFromString(string exposureString)
        {
            foreach (PExposure exposure in exposures)
            {
                if (exposure.ToString() == exposureString) return exposure;
            }
            return null;
        }

        public override string ToString()
        {
            return string.Join("_", name, rightAscension, declination, skyRotation, mechanicalRotation, exposureSelectionPriority);
        }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PExposureSelectionPriority
    {
        [EnumMember(Value = "COMPLETION")]
        COMPLETION,
        [EnumMember(Value = "N_COMPLETION")]
        N_COMPLETION,
        [EnumMember(Value = "SELECTIVITY")]
        SELECTIVITY,
        [EnumMember(Value = "N_SELECTIVITY")]
        N_SELECTIVITY
    }
}
