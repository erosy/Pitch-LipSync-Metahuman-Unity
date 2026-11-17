# Ethan V2 camera-facing confident idle

The CharacterTesting scene's EthanV2_Candidate now uses the separate
`Assets/_Sandbox/Data/Activity/Confident Idle Camera.asset` activity. It retains the
copied confident head animation and facial weights, while its expression omits
all `eyeLook*` entries so camera tracking can control eye direction. The original
Confident Idle / Confident Idle Upward assets remain available unchanged.

The scene previously disabled the master gaze switch, pointed SpeechGazeFrame
backward (-Z), and reserved vertical gaze through the upward-eye expression.
The repaired helper sits between FACIAL_L_Eye and FACIAL_R_Eye, points forward
(+Z in the character's neutral frame), and follows the head bone. Master gaze and
idle tracking are enabled. Head assistance is disabled to preserve the chin pose.

Measured full-weight eye rotations were approximately 34.2–35.1 degrees downward,
24.4–26.3 upward, and 38.5–42.1 horizontally. This scene's camera is below the
raised eyes, so the vertical range uses the downward calibration (34.67 degrees),
with 40.25 degrees horizontally. The single vertical range remains an approximation
for other camera heights; the measurement uses the forward-most eyeball vertex.

`repair.json`, `measured_eye_ranges.json`, and `verification.json` record the
configuration and five isolated samples across the confident idle loop. The
camera requires approximately 41–46 downward weight and 7–9 horizontal weight;
upward weights are zero. The head pose is unchanged during eye tracking.

Verification used disposable prefab contents and manual PlayableGraph evaluation
in Edit Mode. No automatic Play Mode transition, hair simulation, or build was run.
Review the live eye contact by starting Play Mode yourself.

Authoring/verification: `Tools/EthanV2/RepairCameraIdle.cs` via Pipeline run_script,
entry points `RepairCameraIdle.Run`, `.MeasureEyeRanges`, and `.Verify`.
`Tools/EthanV2/InspectLiveGaze.cs` reads live scene configuration without changing it.
