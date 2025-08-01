﻿# DynamicSequencer
A plugin for [NINA](https://nighttime-imaging.eu) that dynamically selects an optimal target for imaging, allowing for unattended, multi-night, multi-target automated imaging.

This plugin draws heavy inspiration from Tom Palmer's [Target Scheduler](https://tcpalmer.github.io/nina-scheduler/) plugin and shares a number of similar features. However, DS is designed for the more advanced user who is looking for more control over their sequencer. DS requires prior experience with NINA and the advanced sequencer to get working properly. However, once functioning, it allows you to queue up a whole season's worth of targets and it will automatically choose a target every night, take exposures, filter out bad exposures, and take flats when a project is completed.

DS is designed for setups that are hosted at a permanent or remote observatory, heavily focusing on long-term automation. Although it will certainly work on a mobile setup, an electronic rotator, filter wheel, and flat panel are recommend to get the most out of this plugin.

DS adds a series of sequencer instructions such as `DS: Center and Rotate` and `DS: Take Exposure` that act similarly to vanilla NINA instructions but are more dynamic, hence the name of the plugin. When configured correctly, DS selects a project from a pool of projects and takes images automatically as defined by the JSON project files.

The use of JSON project files allow for easy integration with other tools. Simple scripts can be written to convert lists of coordinates and rotations to the JSON format required by DS. This may be particularly helpful when planning large mosaics or sky surveys. There is no graphical configuration interface within NINA for DS, everything is done via project JSON files.

> **DISCLAIMER**
> This plugin is in early stages of development and comes with absolutely no warranty. The author of this plugin is not responsible for any potential damage to equipment or lost imaging time. Use at your own risk.


## Getting Started
After running NINA with DynamicSequencer for the first time, files and directories are generated in the `Documents\DynamicSequencer` directory. This includes the `Projects` directory, `Logs` directory, and `settings.json` file.


### **Plugin Settings**
Global plugin settings are located in the `settings.json` file. Settings are listed in the table below. The file generated by the plugin uses default values.

| Setting | Type | Description | Default Value |
| --- | --- | --- | --- |
| logDebug | bool | Enable debug logging. Only enable when actively diagnosing issue as the debug logger is *very* verbose and creates large log files. | `false` |
| projectSelectionPriority | list | Criteria for selecting the best project. See `Notes on Automatic Target Selection` below. | `[ "PRIORITY", "COMPLETION" ]` |


### **Create a Project**
1. Create a project JSON file in the projects directory, it can have any name.
2. Edit the JSON file with the items from the following table, each project will have its own JSON file. **All properties are required.** If a file does not contain a particular property, the property will be populated automatically with its default value.
> **WARNING** There are currently no checks for valid project JSON. All JSON files within the `Projects` directory are loaded by the plugin and assumed to be valid. No warnings may be generated and unexpected behavior, including infinite error loops and crashes, may result from an invalid JSON file.

| Property | Type | Description | Default Value |
| --- | --- | --- | --- |
| name | string | Name of the project | `"Default Project"` |
| active | bool | Project is active when `true`. Automatically set to `false` when a project is completed. | `false` |
| priority | int | Project priority, lower number is higher priority. | `0` |
| ditherEvery | int | Amount of frames to take of each filter before dithering. Set to `0` to disable. | `1` |
| minimumAltitude | double | Minimum altitude (in degrees) of a target for it to be considered. Set to `0` to disable. | `0.0` |
| horizonOffset | double | Minimum altitude (in degrees) above the custom horizon of a target for it to be considered. Disabled if no custom horizon file exists. | `0.0` |
| centerTargets | bool | When set to `false`, `DS: Center and Rotate` will skip centering and will only slew then rotate. This may save some time if you have an accurate pointing model. | `true` |
| useMechanicalRotation | bool | When set to `true`, `DS: Center and Rotate` will save the mechanical rotator position and reuse it instead of platesolving and rotating every time. This saves some time and helps with repeatable flat frames. | `false` |
| flatType | string | Configures when to automatically take flats. See `Notes on Dynamic Flats` below. | `"UPON_PROJECT_COMPLETION"` |
| flatAmount | int | Amount of flat frames to take, per filter, per target. | `0` |
| takeFlatsOverride | bool | When set to `true`, the `DS: Take Trained Flats` instruction will take flats regardless of the `flatType` setting. Reverts back to `false` when flats are successfully taken.  | `false` |
| imageGrader.minStars | int | Minimum amount of stars for a frame to pass. | `-1` |
| imageGrader.maxHFR | double | Maximum average star HFR (in pixels) for a frame to pass. | `100.0` |
| imageGrader.maxGuideError | double | Maximum guiding RMS error (in pixels) for a frame to pass. | `100.0` |
| targetSelectionPriority | list | Criteria for selecting the best target. See `Notes on Automatic Target Selection` below. | `[ "COMPLETION", "ALTITUDE" ]` |
| targets | list | | |
| targets.name | string | Name of the target | `"Default Target"` |
| targets.rightAscension | double | RA of the target in degrees, J2000 | `0.0` |
| targets.declination | double | Dec of the target in degrees, J2000 | `0.0` |
| targets.skyRotation | double | Sky orientation of the target in degrees. This is ignored when `useMechanicalRotation = true` and when there is a valid mechanical rotation value in `targets.mechanicalRotation` | `0.0` |
| targets.mechanicalRotation | double | Mechanical orientation of the rotator in degrees. When `useMechanicalRotation = true` and set to a value less than zero, the planner will attempt to figure out this value when this target is first selected and will save it to the json file for future use. All subsequent slews and rotates on this target will use this value. If there is a major change in the optical train that renders this value inaccurate, it may be reset by simply changing the value to a number below zero again. | `-1.0` |
| targets.takeFlatsOverride | bool | Same behavior as `takeFlatsOverride` but only applies to one target.  | `false` |
| exposureSelectionPriority | list | Criteria for selecting the best exposure. See `Notes on Automatic Target Selection` below. | `[ "SELECTIVITY", "N_COMPLETION" ]` |
| targets.exposures | list | | |
| targets.exposures.filter | string | Name of filter, name must match the configured filter name on the filter wheel. | `""` |
| targets.exposures.exposureTime | double | Exposure time in seconds for the chosen filter. | `60.0` |
| targets.exposures.gain | int | Camera gain. | `0` |
| targets.exposures.offset | int | Camera offset. | `0` |
| targets.exposures.binning | int | Camera binning, 1 for 1x1, 2 for 2x2, etc. | `1` |
| targets.exposures.moonSeparationAngle | double | Moon separation angle in degrees for the Lorentzian curve. Valid range `0-180`. | `0.0` |
| targets.exposures.moonSeparationWidth | int | Moon separation width in days for the Lorentzian curve. Valid range `0-14`. | `0` |
| targets.exposures.requiredAmount | int | Amount of frames required. | `0` |
| targets.exposures.acceptedAmount | int | Amount of frames taken. This value is automatically updated by the plugin as more frames are taken. | `0` |

### **Sample Project JSON File**
The following project file defines an Andromeda Galaxy LRGBHa project:
```json
{
  "name": "M31",
  "active": true,
  "priority": 0,
  "ditherEvery": 1,
  "minimumAltitude": 0.0,
  "horizonOffset": 5.0,
  "centerTargets": true,
  "useMechanicalRotation": true,
  "flatType": "UPON_PROJECT_COMPLETION",
  "flatAmount": 30,
  "takeFlatsOverride": false,
  "imageGrader": {
    "minStars": 100,
    "maxHFR": 2.0,
    "maxGuideError": 1.0
  },
  "targetSelectionPriority": [
    "COMPLETION",
    "ALTITUDE"
  ],
  "targets": [
    {
      "name": "Panel 1",
      "rightAscension": 11.0029,
      "declination": 41.3956,
      "skyRotation": 55.0,
      "mechanicalRotation": -1.0,
      "takeFlatsOverride": false,
      "exposureSelectionPriority": [
        "SELECTIVITY",
        "N_COMPLETION"
      ],
      "exposures": [
        {
          "filter": "L",
          "exposureTime": 300.0,
          "gain": 100,
          "offset": 30,
          "binning": 1,
          "moonSeparationAngle": 140.0,
          "moonSeparationWidth": 10,
          "requiredAmount": 120,
          "acceptedAmount": 0
        },
        {
          "filter": "R",
          "exposureTime": 300.0,
          "gain": 100,
          "offset": 30,
          "binning": 1,
          "moonSeparationAngle": 140.0,
          "moonSeparationWidth": 10,
          "requiredAmount": 60,
          "acceptedAmount": 0
        },
        {
          "filter": "G",
          "exposureTime": 300.0,
          "gain": 100,
          "offset": 30,
          "binning": 1,
          "moonSeparationAngle": 140.0,
          "moonSeparationWidth": 10,
          "requiredAmount": 60,
          "acceptedAmount": 0
        },
        {
          "filter": "B",
          "exposureTime": 300.0,
          "gain": 100,
          "offset": 30,
          "binning": 1,
          "moonSeparationAngle": 140.0,
          "moonSeparationWidth": 10,
          "requiredAmount": 60,
          "acceptedAmount": 0
        },
        {
          "filter": "Ha",
          "exposureTime": 600.0,
          "gain": 100,
          "offset": 30,
          "binning": 1,
          "moonSeparationAngle": 120.0,
          "moonSeparationWidth": 10,
          "requiredAmount": 30,
          "acceptedAmount": 0
        }
      ]
    }
  ]
}
```


### **Notes on Automatic Target Selection**
* The Planner Engine selects an optimal project, target, and exposure from the pool every time any DS instruction is executed.
* The engine will maintain a memory of the previous project and target and will always prioritize those as long as they are valid. This memory may be manually cleared at any time with `DS: Reset Memory`.
* A project is deemed to be valid when: it is active, not completed, and contains at least one valid target.
* A target is deemed to be valid when: is it not completed, above the minimum altitude and custom horizon, and contains at least one valid exposure.
* An exposure is deemed to be valid when: it is not completed and is sufficiently far away from the moon.
* Project, target, and exposure rankings can be fully customized with the `projectSelectionPriority`, `targetSelectionPriority`, and `exposureSelectionPriority` settings, respectively.

Criteria available for `projectSelectionPriority`:
| Key | Description |
| --- | --- |
| `PRIORITY` | Priority number for a project. Lower number is higher priority. |
| `COMPLETION` | Percentage completion of the project. Higher completion is higher priority. |
| `N_COMPLETION` | Inverse of `COMPLETION`. Lower completion is higher priority. |

This setting does nothing if only one project is available/active/valid.

Criteria available for `targetSelectionPriority`:
| Key | Description |
| --- | --- |
| `COMPLETION` | Percentage completion of the target. Higher completion is higher priority. |
| `N_COMPLETION` | Inverse of `COMPLETION`. Lower completion is higher priority. |
| `ALTITUDE` | Current altitude of target. Higher altitude is higher priority. |
| `N_ALTITUDE` | Inverse of `ALTITUDE`. Lower altitude is higher priority. |

This setting does nothing if only one target is available/valid.

Criteria available for `exposureSelectionPriority`:
| Key | Description |
| --- | --- |
| `COMPLETION` | Percentage completion of the exposure. Higher completion is higher priority. |
| `N_COMPLETION` | Inverse of `COMPLETION`. Lower completion is higher priority. |
| `SELECTIVITY` | The selectivity of the exposure (`= moonSeparationAngle * moonSeparationWidth`). Higher selectivity is higher priority. |
| `N_SELECTIVITY` | Inverse of `ALTITUDE`. Lower selectivity is higher priority. |

This setting does nothing if only one exposure is available/valid.

The criterion with a lower index in the list takes precedence. If a previous criterion is able to discern two projects/target/exposures, later criteria will not be checked.
For example, if `projectSelectionPriority` is `[ "PRIORITY", "COMPLETION" ]`, the project with a lower priority number will always be selected, given that it is active and valid, regardless of its completion. If two projects have the same priority number, the more completed project will be selective first.

Conversely, if `projectSelectionPriority` is `[ "COMPLETION", "PRIORITY" ]`, the project that is more completed will always be selected, given that it is active and valid, regardless of priority number. If two projects have the same completion percentage, the project with a higher priority number will be selected first.

If all listed criteria are the same for all projects, a random project will be chosen.

> **WARNING** Target selection behavior is undefined if the lists are left blank.


### **Notes on Dynamic Flats**
* DS keeps an internal log of all exposures that have passed image grading to determine which project, target, and/or filter requires flats to be taken. `DS: Take Trained Flats` takes those flats and clears the log.
* Dynamic flats will only function if *all* of the following prerequisites are satisfied: `flatAmount` greater than zero, `useMechanicalRotation = true`, `target.mechanicalRotation` is a valid value, and trained flat presets are available for the specific filter, gain, and binning. If any of the prerequisites are not met, that particular project / target / exposure will be ignored.
* Once the criterion specified in the `flatType` field is satisfied, `flatAmount` number of flats will be taken for each applicable project, target, and exposure upon the execution of the `DS: Take Trained Flats` instruction. For example, if `flatType = "UPON_PROJECT_COMPLETION"` and `flatAmount = 30` for a particular project, after the project reaches 100% completion the next time `DS: Take Trained Flats` is executed, 30 flats will be taken for every target and exposure combination in this project.
* Only one option can be chosen for `flatType`.
* If `takeFlatsOverride` is set to `true`, `flatAmount` number of flats will be taken for each applicable project, target, and exposure upon the execution of the `DS: Take Trained Flats` instruction regardless.

Options available for `flatType`:
| Key | Description |
| --- | --- |
| UPON_PROJECT_COMPLETION | All target and exposure combinations are added to the flat log when the parent project reaches 100% or more completion. |
| UPON_TARGET_COMPLETION | All exposures are added to the flat log when the parent target reaches 100% or more completion. |
| NIGHTLY | All projects, targets, or exposures that received additional data (that passed image grading) are added to the flat log. |
| NONE | Disables automatic flats. |

> **WARNING** Automatic flat system behavior is undefined if the option is left blank.


### **Notes on Image Grading**
* Frames that are rejected do not increment the `acceptedAmount` field.
* There is no option to turn off image grading, it can be effectively disabled by setting the grading criteria such as `"minStars": -1`.
* Frames that are rejected are not deleted. Instead, the `Sequence Title` attribute of the frame metadata is edited to have `- REJECTED` at the end. If NINA is configured to include the `$$SEQUENCETITLE$$` attribute in the image file pattern setting, the image's name will change accordingly. For example, if a frame is usually saved as `M31/Panel 1/Ha_2000_01_01.fits`, a rejected frame will be `M31 - REJECTED/Panel 1/Ha_2000_01_01.fits`.


### **Configure the Advanced Sequencer**
All Dynamic Sequencer items can be identified by a name which starts with `DS:`. All the sequencer instructions and conditions from this plugin can be used in the same way as any other instruction from NINA.

#### `DS: Center and Rotate`
Slew, centers, and rotates to the optimal target. Current project and current target is saved in memory. The planning engine is "sticky", it will always prioritize the current project and current target if they are incomplete and available. This prevents the planner from bouncing back and forth between two or more targets, wasting valuable time. Will do nothing if the current target did not change. Behavior similar to vanilla NINA's `Slew, Center, and Rotate` can be achieved by setting `centerTargets = true` and `useMechanicalRotation = false`.

#### `DS: Reset Memory`
Clears memory of current project and current target. The planning engine will reselect the optimal target with no "stickiness" during the next run. This should be run at the end of every night. May be run more frequently if shooting mosaics.

#### `DS: Take Exposure`
Takes one exposure in accordance to the exposure plan selected by the planning engine.
> **WARNING** `DS: Take Exposure` makes no attempt to check if the imaging setup is in the correct configuration (mount pointed at correct object, filter wheel set to correct filter, flat panel open, etc.). As long as the frame taken passes image grading, it will increment the `acceptedAmount` field for the target *it thinks* its pointed at. For example, an imaging setup is previously imaging M31, and the Planner Engine selects a new target M42, if for whatever reason the mount did not slew properly or the `DS: Center and Rotate` instruction was skipped, the subsequent `DS: Take Exposure` instruction will increment the `acceptedAmount` field for M42 and place the frame into the M42 folder while it is actually still shooting M31. The same behavior is true for the filter wheel. Hence, it is generally good practice to set the `On error` option to `Skip current instruction set` within NINA for all DS-related instructions.

#### `DS: Switch Filter`
Switches filter in accordance to the exposure plan selected by the planning engine.

#### `DS: Dither`
Sends a dither command to the guiding software when conditions are met. Configurable with the `ditherEvery` setting. This instruction will only send a dither command if the number of frames shot in *any filter* since the last dither/slew exceeds the setting. For example, when `ditherEvery = 1`, a series of frames may look something like this: `SHO'SHO'SHO` or `L'L'LRGB'L'L'LRGB`. Or when `ditherEvery = 2` it may look like: `SHOSHO'SHOSHO` or `LL'LRGBL'LLRGB'LL`.

#### `DS: Take Trained Flats`
Checks the internal flat log for required flats. Configured with `flatType` and `flatAmount` fields. It is recommended to run this instruction once a day during daytime. The instruction will be skipped if there is nothing to do.

#### `DS: Wait Until Target Available`
Waits indefinitely until at least one target is returned from the planning engine. Note: the planning engine does not filter for sun altitude, so this should be used along with `Wait if Sun Altitude` or `Wait for Time`.

#### `DS: Loop While Target Available`
Loops indefinitely while at least one target is returned from the planning engine. Note: the planning engine does not filter for sun altitude, so this should be used along with `Loop Until Sun Altitude` or `Loop Until Time`.

#### `DS: Loop While Project Available`
Loops indefinitely while at least one project is active. Completed projects are automatically set to inactive.

An example sequence:
![ExampleSequence](resources/ExampleSequence.png)


## Changelog
### v3.5.1.0
- Added option to override flats for each individual target

### v3.5.0.0
- Now targeting NINA 3.0.0.3005-rc
- Redesigned dynamic flat system

### v3.4.0.0
- Replaced target selection priority logic
- Added new moon separation priority criterion
- Targets and exposures list order within project JSON files is now static
- Project JSON files now have default values

### v3.3.0.0
- Bumped to .NET 8 to target NINA 3.0.0.1085-nightly
- Project coordinates property now use J2000 epoch
- Normal (non-debug) log files are now slightly more detailed
- Fixed 180 degree rotation logic
- Fixed potential target mismatch between DynamicCenterAndRotate and DynamicExposure

### v3.2.0.0
- Projects are now located under `Documents\DynamicSequencer\Projects`
- Added `settings.json` file
- Added logger and option to enable debug logging

### v3.1.1.0
- Added option to disable dithering
- Fixed AF trigger issue with DynamicExposure (again)

### v3.1.0.0
- Ported to NINA 3
- Versioning is now NINA_MAJOR.MAJOR.MINOR.PATCH

### v0.5.0.0
- Added save mechanical rotation feature
- Added sequencer item validations
- Added `DS: Take Trained Flats`
- Updated README introduction section

### v0.4.0.0
- Added `DS: Dither`
- Projects, targets, and exposures now have unique ids

### v0.3.1.0
- Fixed AF trigger issue with DynamicExposure

### v0.3.0.0
- Added image grader
- Fixed meridian flip timing issue

### v0.2.0.0
- Added `DS: Loop While Project Available`

### v0.1.0.0
- Added planning engine
- Added moon separation filter
- Added `DS: Center and Rotate`
- Added `DS: Reset Memory`
- Added `DS: Take Exposure`
- Added `DS: Switch Filter`
- Added `DS: Wait Until Target Available`
- Added `DS: Loop While Target Available`
