# Ethan V2 — original-solver facial bake

## Installed model performance note - 16 November 2026

The optimized production head also has an expression-shading correction for
dark blink wrinkles. See [normal repair and validation](../NormalRepair/README.md).

The production prefab now uses an optimized head mesh derivative rather than
the dense original FBX head. See [installed optimization](../FacialHairOptimization/README.md).
The original sources below remain preserved for authoring and recovery.

The installed Unity FBX also contains the later mesh-eyebrow/eyelash conversions,
which add 321,216 vertices to the 73-shape head. A read-only audit found these
facial-hair sections contribute 622,356 triangles, while the scalp already uses
32 points/30 Hz. See [current performance findings](../PerformanceAudit/README.md)
before treating the original native bake or scalp sampling rate as the cause.


## Review files

- `Ethan_V2_73_Baked.blend`: baked character with the project's exact 73 ordered
  blendshape names. Opens without Polyhammer or DNA evaluation.
- `Ethan_V2_FaceBoard_Restored.blend`: migrated authoring character with the
  original MetaHuman board and restored DNA correction shapes. Requires the
  installed Character DNA extension. Keep its adjacent `Dependencies` directory.
- `Previews`: clay renders for checking geometry, independent of texture shading.
- `reopen_verification.json`: independent saved-file validation results.

Saved-file validation passed all **962 mesh/shape comparisons**, including
neutral: maximum error **0.000835 mm** against the evaluated native-solver bake
samples. Ordered names, preserved skin weights, neutral reset, and groom control
points/world frames passed. The baked file was checked without Polyhammer.

Open the baked file in Blender 5.2. Select `BAKED_head_lod0_mesh`, then Object
Data Properties > Shape Keys. Set one key from 0 to 1, then reset it before
trying another. Teeth, inner lips/tongue, eye, eyelash and saliva mesh keys follow
the head automatically. `viseme_sil` is intentionally neutral. `Basis` is not
one of the 73 expression names.

Do not enable or unhide `SOURCE_AUTHORING - preserved, hidden` when inspecting
the baked character. It is an archive. Do not animate the frozen facial bones
on top of the same baked shape keys. For body animation/export, the multiple
authoring armatures still require a separate skeleton-consolidation step.

The restored file retains the delivered Rigify body setup. To inspect the
MetaHuman board, enable Character DNA, select the face-board armature in Pose
Mode and move its controls. Use Character DNA > Rig Instances > Force Evaluate
if a newly opened session has not initialized its authoring cache yet. The
restored file includes the modeller's split mouth meshes; their corrective
values follow the main head.

## Bake method and scope

This is original Polyhammer native RigLogic output from Ethan's supplied DNA,
not poses sampled from the approximate Rigify face. Native facial bone motion
and DNA correction deltas are evaluated together before baking. The 52 facial
motion names map to corresponding MetaHuman controls. The 15 visemes and six
emotions map to the installed Polyhammer face-board pose library. The reference
tongue presets' full jaw opening is reduced for TH, kk, SS and nn speech shapes.
These mappings preserve the current project's names and order; they do not
reproduce old Ethan's vertex deltas or recover animation clips.

`mouthClose` is an additive lip-seal delta: jaw-open with LipsTogether minus
jaw-open alone. Use it with `jawOpen` to close the lips while the jaw is open.
It is sampled through RigLogic rather than stored as a zero placeholder.

The modeller removed/reindexed and split parts of the head. Original DNA
corrective deltas were matched by neutral vertex positions, while preserving
the delivered geometry and weights. The maximum correspondence distance is
0.274 mm; the teeth and other eye meshes retain their original vertex ordering.
See `corrective_restoration.json`. Review inner lips and tongue in particular.

The bake inverts each mesh's neutral skinning stack so its saved keys reproduce
the solver samples without applying facial bone motion twice. Separate frozen
neutral armatures retain vertex groups, skin weights and material slots.

The three supplied Blender grooms retain their original control points, counts,
and world frames. Existing surface references are redirected to the baked head.
This task does not import grooms into Unity, resample them, or change simulation.
Future Unity groom integration must use the project's 32-point/30 Hz defaults.

The original Downloads files and active Unity character/prefab are unchanged.
No Unity Play Mode transition or build was run. Numerical checks prove bake
fidelity; artist review of mouth seals, eyelid contact, gaze, teeth/tongue contact,
emotions and expression combinations is still required.

## Reproducibility

Sources: `C:/Users/ina17/Downloads/Ethan2_Ver2/Ethan_Ver2.blend` and
`C:/Users/ina17/Downloads/Ethan_Ver2_NewMetaHumanCharacter/Ethan_NewMetaHumanCharacter`.
The latter's manifest identifies Ethan; DNA hashes are in `dna_validation.json`.

Tools are under `Tools/EthanV2`: `reconnect_solver.py`,
`restore_native_correctives.py`, `native_pose_mapping.py`, and
`bake_native_shapes.py`. The baked file deliberately refuses overwrite.
`shape_pose_mapping.json` records all GUI values and preset provenance.
`evaluated_baselines.npz` records original evaluated mesh samples.

Reopen the baked file with Blender's `--factory-startup --disable-autoexec`, run
`Tools/EthanV2/bake_native_shapes.py`, and append `-- --verify` to compare every
saved shape and neutral geometry with those samples, without the addon loaded.
The first untuned draft is retained under `Iterations` for provenance; use the
top-level baked file for review.
