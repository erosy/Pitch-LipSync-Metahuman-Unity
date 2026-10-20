# Jiwoo reimport — Batch 2 prepared Blender scene

## Result

Prepared copy: `Data/JiwooReimport/Batch02/Jiwoo_ExportPrepared.blend`.
Open it with Blender 5.1. The original Downloads file is unchanged, verified by
SHA-256. Unity assets and settings were not changed in this batch.

The prepared scene contains only `Jiwoo_Armature`, the combined character mesh
`Face`, and native hair curves `Hair`. Authoring rigs, duplicate objects, native
DNA driver objects, and embedded Text scripts were removed from this copy.
The preserved export pair has no rig constraints, rig drivers, shape-key drivers,
or non-neutral bone poses. Seventeen usable material images were packed into the
copy. Existing material assignments are preserved; Unity material setup remains
a later step.

## Blender settings and coordinate contract

- Metric units, unit scale 1, meters; neutral frame 1.
- Rig and mesh object scale 1; existing skeleton and bind geometry are preserved.
- Hair control points and both Bezier handles are transformed into the export
  head bone's local coordinates, in meters. Their world-space fit is preserved.
- Hair now follows `Jiwoo_Armature/head` instead of the authoring head rig.
  Its local basis is identity. Blender's bone-tail parenting offset is compensated
  in the parent inverse; retain this relationship in the prepared scene.
- Explicit radius: 0.00005 m, meaning diameter 0.1 mm. This is the chosen initial
  groom width, not a value recovered from the original groom. Width/taper tuning
  can follow after export/import dimensions are proven.
- Character FBX selection is `Jiwoo_Armature` and `Face`; hair exports separately.
- FBX export axis/unit options and Alembic frame/width conversion have not yet
  been finalized or validated. Unit scale 1 in Blender alone does not prove that
  imported Unity bones will have unit scale.

## Verification

Preparation compared all character shape-key coordinate buffers, bone hierarchy
and rest matrices, deform flags, skin weights, vertex groups, and material slots
against the source. An independent Blender process reopened the saved copy and
repeated the comparisons.

- Character: 64,300 vertices, 1,185 bones, 74 shape keys including Basis retained.
- Groom: all 100,493 curves and 1,353,886 control points retained, with native
  Bezier handles preserved.
- Evaluated neutral character vertex displacement: 0 m.
- Maximum world displacement across all hair points/handles: approximately
  0.0001444 mm, below the 0.001 mm tolerance.
- A temporary 15-degree head rotation carried the groom as expected; maximum
  sampled-computation error across **all** control points and handles was about
  0.00003775 mm. The pose was restored, and this test did not save file changes.
- Explicit radius attribute persisted through save/reopen.

`preparation_report.json`, `verification_report.json`, and the source geometry
baseline in the Batch02 directory record these checks. `prepared_preview.png`
is a Blender Workbench inspection render with temporary neutral display colors;
it illustrates shape and fit, not final Unity shading or simulation.

## Reproduction

Run `Tools/Hair/prepare_jiwoo_export.py` in Blender background mode with
`--factory-startup --disable-autoexec --python-exit-code 1`.
Open the original source and pass `-- prepare`, or open the prepared file and
pass `-- verify`. Preparation refuses to overwrite an existing prepared file.
`Tools/Hair/preview_jiwoo_prepared.py` renders the preview from the prepared file
without saving changes to it.

## Next batch and handoff

Batch 3 will generate FBX/ABC outside Unity Assets and verify scale, axes,
head-relative attachment, evaluated curve shape, width semantics, skinning, and
shape keys through export/readback checks. Preserve the original native Bezier
curves in the prepared file if export requires a sampled derivative.

No Unity import action is required yet. After Batch 3 passes, provide the user
the verified files and exact Unity import settings for a new Jiwoo variant.
The user performs all Unity steps and controls Play Mode.
