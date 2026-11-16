# Clothing seam weight repair

Applied to `Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx`, used by
the saved `Jiwoo_Normalized.prefab` and its current scene instance. Model and
prefab GUIDs, Humanoid mapping/reference pose, material remaps, and character
animation/lip-sync/flow references are preserved. No Play Mode transition,
animation-clip playback, or build was performed.

The repair assigns identical averaged, normalized weights to near-coincident
garment boundary vertices in 489 seam clusters (524 vertex pairs). This changes
963 of the outfit's 5,038 vertices and eliminates all 505 mismatched pairs.
No vertices are moved, welded, added, or removed. Existing openings and UV
islands are retained. Independent FBX readback confirms unchanged topology,
UVs, material slots, all 73 shape coordinates, and Face weights; maximum outfit
weight export error is zero. Weight normalization at the seams is the only
intended model change.

Unity stress checks on disposable copies, using the same 20-degree local-Z bone
rotations as the diagnostic audit:

| Bone | Maximum gap before | Maximum gap after | Pairs over 1 mm after |
| --- | ---: | ---: | ---: |
| Right shoulder | 33.67 mm | 0.0985 mm | 0 |
| Left shoulder | 34.17 mm | 0.0987 mm | 0 |
| Upper chest | 31.99 mm | 0.0987 mm | 0 |

The small remaining gaps are the garment's original neutral vertex offsets,
retained because this repair changes weights only. These tests establish seam
continuity under the tested skeletal poses; they do not certify every animation
or eliminate separate skin poke-through/garment-fit issues.

`Jiwoo_EthanSetup_SeamFixed.blend` is the new editable repaired copy. The earlier
prepared Blender file and original modeller delivery remain unchanged. The
Unity model is updated from this folder's `Jiwoo_EthanSetup.fbx`; use this repaired
copy for future clothing edits/exports so old seam weights are not restored.

`Before` preserves the prior FBX, importer metadata, prefab, and initial audit.
`repair_report.json` lists every affected vertex and its before/after weights.
`blender_readback.json`, `unity_install.json`, `unity_stress_after.json`, and
`prefab_verification_command.json` record the validation and installation.
The already-dirty open scene was not saved automatically.

Reproduction tools under `Tools/Hair`: `repair_jiwoo_clothing_seams.py`,
`verify_jiwoo_clothing_seams.py`, `InstallJiwooClothingSeams.cs`, and
`AuditJiwooClothing.AfterRepair`. The Blender generator writes a separate file
and refuses to overwrite it. The Unity installer requires successful FBX
readback and runs only in Edit Mode.
