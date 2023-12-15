﻿using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

using NINA.Astrometry;
using NINA.Profile.Interfaces;

namespace DanielHeEGG.NINA.DynamicSequencer.PlannerEngine
{
    public class Planner
    {
        public List<PProject> _projects { get; }

        public Planner()
        {
            _projects = new List<PProject>();

            foreach (string filename in Directory.GetFiles(DynamicSequencer.projectDir, "*.json"))
            {
                using (StreamReader r = File.OpenText(filename))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    PProject project = (PProject)serializer.Deserialize(r, typeof(PProject));
                    project.filename = filename;
                    _projects.Add(project);

                    DynamicSequencer.logger.Debug($"Planner: loaded project '{project.name}', filename '{project.filename}'");
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

                    DynamicSequencer.logger.Debug($"Planner: wrote project '{project.name}', filename '{project.filename}'");
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
                DynamicSequencer.logger.Debug($"Planner: filtering project '{project.name}'");

                if (project.completion >= 1.0f)
                {
                    project.active = false;
                    project.valid = false;

                    DynamicSequencer.logger.Debug($"Planner: rejected (completed)");

                    continue;
                }

                if (!project.active)
                {
                    project.valid = false;

                    DynamicSequencer.logger.Debug($"Planner: rejected (inactive)");

                    continue;
                }

                project.Filter(profileService, time, location);

                if (!project.valid) DynamicSequencer.logger.Debug($"Planner: rejected (no valid targets)");
            }
        }

        public PProject Best()
        {
            if (_projects.Count == 0)
            {
                DynamicSequencer.logger.Debug($"Planner: no project selected (empty list)");

                return null;
            }

            foreach (PProject project in _projects)
            {
                if (project.valid && project.ToString() == DynamicSequencer.currentProject)
                {
                    DynamicSequencer.logger.Debug($"Planner: project '{project.name}' selected (previous project)");

                    return project;
                }
            }

            _projects.Sort(delegate (PProject x, PProject y)
            {
                int prioValid = Convert.ToInt32(y.valid) - Convert.ToInt32(x.valid);
                return prioValid == 0 ? (x.priority == y.priority ? (int)((y.completion - x.completion) * 1000) : x.priority - y.priority) : prioValid;
            });

            if (_projects[0].valid)
            {
                DynamicSequencer.logger.Debug($"Planner: project '{_projects[0].name}' selected (best project)");

                return _projects[0];
            }

            DynamicSequencer.logger.Debug($"Planner: no project selected (no valid project)");

            return null;
        }

        public PProject GetProjectFromString(string projectString)
        {
            foreach (PProject project in _projects)
            {
                if (project.ToString() == projectString) return project;
            }
            return null;
        }
    }
}
