using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Profile.Interfaces;

namespace DanielHeEGG.NINA.DynamicSequencer.PlannerEngine
{
    [JsonObject(MemberSerialization.OptOut)]
    public class PProject
    {
        public string name { get; set; }
        public bool active { get; set; }
        public int priority { get; set; }
        public int ditherEvery { get; set; }
        public double minimumAltitude { get; set; }
        public double horizonOffset { get; set; }
        public bool balanceTargets { get; set; }
        public bool centerTargets { get; set; }
        public bool useMechanicalRotation { get; set; }
        public bool takeFlats { get; set; }
        public int flatAmount { get; set; }
        public Grader imageGrader { get; set; }
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
                DynamicSequencer.logger.Debug($"Planner: ---- filtering target '{target.name}'");

                if (target.completion >= 1.0f)
                {
                    target.valid = false;

                    DynamicSequencer.logger.Debug($"Planner: ---- rejected (completed)");

                    continue;
                }

                double targetAltitude = AstrometryUtils.GetAltitude(location, target.rightAscension, target.declination, time);
                double targetAzimuth = AstrometryUtils.GetAzimuth(location, target.rightAscension, target.declination, time);
                if (targetAltitude < minimumAltitude)
                {
                    target.valid = false;

                    DynamicSequencer.logger.Debug($"Planner: ---- rejected (altitude)");

                    continue;
                }

                CustomHorizon horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;
                if (horizon != null && targetAltitude < horizon.GetAltitude(targetAzimuth) + horizonOffset)
                {
                    target.valid = false;

                    DynamicSequencer.logger.Debug($"Planner: ---- rejected (horizon offset)");

                    continue;
                }

                target.Filter(profileService, time, location);

                if (!target.valid) DynamicSequencer.logger.Debug($"Planner: ---- rejected (no valid exposures)");
            }
        }

        public PTarget Best()
        {
            if (targets.Count == 0)
            {
                DynamicSequencer.logger.Debug($"Planner: ---- no target selected (empty list)");

                return null;
            }

            foreach (PTarget target in targets)
            {
                if (target.valid && target.ToString() == DynamicSequencer.currentTarget)
                {
                    DynamicSequencer.logger.Debug($"Planner: ---- target '{target.name}' selected (previous target)");

                    return target;
                }
            }

            // valid targets placed by reference in separate list to preserve the order in "targets"
            List<PTarget> validTargets = new List<PTarget>();
            foreach (PTarget target in targets)
            {
                if (target.valid) validTargets.Add(target);
            }

            validTargets.Sort(delegate (PTarget x, PTarget y)
            {
                if (balanceTargets) return (int)((x.completion - y.completion) * 1000);
                return (int)((y.completion - x.completion) * 1000);
            });

            if (validTargets.Count != 0 && validTargets[0].valid)
            {
                DynamicSequencer.logger.Debug($"Planner: ---- target '{validTargets[0].name}' selected (best target)");

                return validTargets[0];
            }

            DynamicSequencer.logger.Debug($"Planner: ---- no target selected (no valid target)");

            return null;
        }

        public PTarget getTargetFromString(string targetString)
        {
            foreach (PTarget target in targets)
            {
                if (target.ToString() == targetString) return target;
            }
            return null;
        }

        public override string ToString()
        {
            return string.Join("_", name, active, priority, ditherEvery, minimumAltitude, horizonOffset, balanceTargets, centerTargets, useMechanicalRotation, imageGrader);
        }
    }
}
