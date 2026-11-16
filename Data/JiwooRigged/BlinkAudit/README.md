# Blink shading diagnosis and import correction

The user recording shows dark jagged marks near the closed eyelid at full
left/right blink weights. Frames were extracted for inspection without changing
the original recording. Diagnostic renders use a disposable copy of the imported
model, baked to static geometry for each pose so preview resets cannot invalidate
the comparison. No live character pose or Play Mode state was changed.

## Findings

- The dark folded-looking artifacts remain when facial mesh hair is hidden.
- They remain when eye shell, eye edge, and cartilage overlays are hidden.
- Disabling the normal texture does not remove them.
- Recalculating normals/tangents from the posed diagnostic mesh removes the
  jagged black folds. Flat unlit rendering also removes the broad shading bands.
  This identifies incorrect posed normal shading as the principal defect.
- The model previously imported its base normals while calculating blend-shape
  normals. Using its authored FBX blend-shape normals instead yields clean
  left/right blink comparisons, without those jagged shading artifacts.

## Applied change

On `Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx`, changed
**Blend Shape Normals: Calculate -> Import**. Base normals remain Import.
No mesh positions, blink weights/shape coordinates, skin weights, textures,
materials, lighting, rig mappings, or character animation components are edited.
The FBX file is byte-identical to the installed clothing-weight repair artifact;
only its Unity import metadata changes. The neutral lit preview is compared
before/after in `integrity_check.json`. Both full-blink lit previews were inspected
after reimport. The ordinary eyelid crease/lash line remains visible; black
shading artifacts are not fixed by erasing that crease or disabling shadows.

`Before/Jiwoo_EthanSetup.fbx.meta` preserves the previous importer.
`Before/Images` contains the old diagnostic images. Current `eyeBlinkLeft_*`,
`eyeBlinkRight_*`, and `neutral_*` images contain the corrected comparisons.
`render_diagnosis.json`, `normal_import_change.json`, `integrity_check.json`, and
`prefab_verification_command.json` record the checks. Saved prefab/current-scene
mesh, Humanoid avatar/T-pose, facial shape, animation-module and lip-sync
references were independently checked after reimport. The open scene was not
saved automatically. No Play Mode transition or build was performed.

Tools: `inspect_jiwoo_blink_video.py`, `AuditJiwooBlink.cs`, and
`RepairJiwooBlinkNormals.ImportAuthored` under `Tools/Hair`.
