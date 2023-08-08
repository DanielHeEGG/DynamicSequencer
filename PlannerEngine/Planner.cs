using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;

namespace DanielHeEGG.NINA.DynamicSequencer.PlannerEngine
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

            foreach (PProject project in _projects)
            {
                if (project.valid && project.ToString() == DynamicSequencer.previousProject)
                {
                    return project;
                }
            }

            _projects.Sort(delegate (PProject x, PProject y)
            {
                int prioValid = Convert.ToInt32(y.valid) - Convert.ToInt32(x.valid);
                return prioValid == 0 ? (x.priority == y.priority ? (int)((y.completion - x.completion) * 1000) : x.priority - y.priority) : prioValid;
            });

            return _projects[0].valid ? _projects[0] : null;
        }
    }
}
