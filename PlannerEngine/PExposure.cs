﻿using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using NINA.Core.Model.Equipment;

namespace DanielHeEGG.NINA.DynamicSequencer.PlannerEngine
{
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

        public override string ToString()
        {
            return string.Join("_", filter, exposureTime, gain, offset, binning, moonSeparationAngle, moonSeparationWidth, requiredAmount);
        }
    }
}
