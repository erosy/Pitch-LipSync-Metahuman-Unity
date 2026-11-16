# Humanoid reference-pose correction

Jiwoo's avatar mapping was valid, but its configured retargeting reference was
the delivered A-pose. Unity's reference T-pose validation failed with pose error
394.16745. This reference mismatch is consistent with the raised/bent arm offset
reported while playing idle and greeting animations.

The avatar reference was corrected using Unity Editor's own pose-authoring
functions on a disposable imported-model copy. Only the FBX importer's
HumanDescription reference skeleton was updated. No source mesh was re-exported
and no animation clips were changed or sampled. The mesh bind pose remains the
delivered bind pose. The importer now authors and validates this reference pose,
and its saved-character checker rejects an invalid reference pose.

After reimport, the saved reference passes T-pose validation with pose error 0.
The avatar remains valid and Humanoid, and all 73 facial shapes remain present.
The importer script compiles; existing selection, Liora profile, viseme, idle,
thinking, and greeting bindings pass static checks. No Play Mode changes,
animation playback, live testing, or builds were performed. The user must retry
greeting and idle to confirm the visible correction.

`result.json` records the before/after result. `importer_before.json` records the
previous importer settings. `Tools/Hair/RepairJiwooAvatar.cs` provides `AuditPose`
and `Apply` entry points; `InstallJiwooRigged.cs` includes the durable import fix.

Unity's documentation describes the required reference pose and Enforce T-Pose:
https://docs.unity3d.com/6000.0/Documentation/Manual/ConfiguringtheAvatar.html
