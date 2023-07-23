﻿using System;
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
        public double minimumAltitude { get; set; }
        public double horizonOffset { get; set; }
        public bool balanceTargets { get; set; }
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
}