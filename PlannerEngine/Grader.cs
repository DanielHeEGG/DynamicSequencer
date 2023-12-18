using Newtonsoft.Json;

using NINA.Image.Interfaces;

namespace DanielHeEGG.NINA.DynamicSequencer.PlannerEngine
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Grader
    {
        public int minStars { get; set; } = -1;
        public double maxHFR { get; set; } = 100;
        public double maxGuideError { get; set; } = 100;

        public bool GradeImage(IImageData imageData)
        {
            if (imageData == null)
            {
                DynamicSequencer.logger.Warning("Grader: no data");

                return false;
            }

            int starCount = imageData.StarDetectionAnalysis.DetectedStars;
            if (starCount < minStars)
            {
                DynamicSequencer.logger.Information($"Grader: rejected, star count {starCount}/{minStars}");

                return false;
            }

            double HFR = imageData.StarDetectionAnalysis.HFR;
            if (HFR > maxHFR)
            {
                DynamicSequencer.logger.Information($"Grader: rejected, HFR {HFR}/{maxHFR}");

                return false;
            }

            double guideError = imageData.MetaData.Image.RecordedRMS.Total;
            if (guideError > maxGuideError)
            {
                DynamicSequencer.logger.Information($"Grader: rejected, guide error {guideError}/{maxGuideError}");

                return false;
            }

            return true;
        }

        public override string ToString()
        {
            return string.Join("_", minStars, maxHFR, maxGuideError);
        }
    }
}
