using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using NINA.Astrometry;
using NINA.Profile.Interfaces;

namespace DanielHeEGG.NINA.DynamicSequencer.PlannerEngine
{
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

        public override string ToString()
        {
            return string.Join("_", name, rightAscension, declination, rotation, balanceFilters);
        }
    }
}
