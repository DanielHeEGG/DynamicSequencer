using System;

using NINA.Astrometry;

namespace DanielHeEGG.NINA.DynamicSequencer
{
    public class AstrometryUtils
    {

        private const double DAYS_IN_LUNAR_CYCLE = 29.53059;

        public static double GetAltitude(ObserverInfo location, double ra, double dec, DateTime atTime)
        {
            double siderealTime = AstroUtil.GetLocalSiderealTime(atTime, location.Longitude);
            double hourAngle = AstroUtil.GetHourAngle(siderealTime, AstroUtil.DegreesToHours(ra));
            double degAngle = AstroUtil.HoursToDegrees(hourAngle);
            return AstroUtil.GetAltitude(degAngle, location.Latitude, dec);
        }

        public static double GetAzimuth(ObserverInfo location, double ra, double dec, DateTime atTime)
        {
            double siderealTime = AstroUtil.GetLocalSiderealTime(atTime, location.Longitude);
            double hourAngle = AstroUtil.GetHourAngle(siderealTime, AstroUtil.DegreesToHours(ra));
            double degAngle = AstroUtil.HoursToDegrees(hourAngle);
            return AstroUtil.GetAzimuth(degAngle, AstroUtil.GetAltitude(degAngle, location.Latitude, dec), location.Latitude, dec);
        }

        public static double GetMoonSeparation(ObserverInfo location, double ra, double dec, DateTime atTime)
        {

            NOVAS.SkyPosition pos = AstroUtil.GetMoonPosition(atTime, AstroUtil.GetJulianDate(atTime), location);
            var moonRaRadians = AstroUtil.ToRadians(AstroUtil.HoursToDegrees(pos.RA));
            var moonDecRadians = AstroUtil.ToRadians(pos.Dec);

            var targetRaRadians = AstroUtil.ToRadians(ra);
            var targetDecRadians = AstroUtil.ToRadians(dec);

            var theta = SOFA.Seps(moonRaRadians, moonDecRadians, targetRaRadians, targetDecRadians);
            return AstroUtil.ToDegree(theta);
        }

        public static double GetMoonAvoidanceLorentzianSeparation(DateTime atTime, double distance, int width)
        {
            if (width == 0)
            {
                width = 1;
            }

            return distance / (1 + Math.Pow((0.5 - (GetMoonAge(atTime) / DAYS_IN_LUNAR_CYCLE)) / (width / DAYS_IN_LUNAR_CYCLE), 2));
        }

        public static double GetMoonAge(DateTime atTime)
        {
            double moonPA = AstroUtil.GetMoonPositionAngle(atTime);
            moonPA = moonPA > 0 ? moonPA : (180 + moonPA) + 180;
            return moonPA * (DAYS_IN_LUNAR_CYCLE / 360);
        }
    }
}
