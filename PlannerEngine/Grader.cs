using Newtonsoft.Json;

using NINA.Image.Interfaces;

namespace DanielHeEGG.NINA.DynamicSequencer.PlannerEngine
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Grader
    {
        public int minStars { get; set; }
        public double maxHFR { get; set; }
        public double maxGuideError { get; set; }

        public bool GradeImage(IImageData imageData)
        {
            if (imageData == null) return false;
            if (imageData.StarDetectionAnalysis.DetectedStars < minStars) return false;
            if (imageData.StarDetectionAnalysis.HFR > maxHFR) return false;
            if (imageData.MetaData.Image.RecordedRMS.Total > maxGuideError) return false;
            return true;
        }

        public override string ToString()
        {
            return string.Join("_", minStars, maxHFR, maxGuideError);
        }
    }
}
