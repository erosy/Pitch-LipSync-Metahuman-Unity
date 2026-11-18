# Liora expression shading repair

18 November 2026. This supersedes the earlier known full-strength Joy shading
limitation in the integration/hierarchy reports.

The repaired mesh is `Assets/_Sandbox/Characters/Liora/Candidate/Liora_Face.asset`.
Its asset GUID and prefab references are preserved. Both the saved Liora candidate
and the active Face in CharacterTesting use this mesh; no scene replacement is
needed.

## Demonstrated cause

Matched diagnostics reproduce the jagged dark bands below the eyes, beside the
nose, and at the lip corners. Disabling the normal map does not remove them.
Isolating the skin from brows/lashes/eye overlays does not remove them. The marks
remain with shadows disabled, but disappear with unlit skin and when the posed
shading normals/tangents are derived from the actual deformed geometry.

This identifies incorrect expression normal/tangent deltas, rather than a faulty
normal texture or shadows. The normal map is already imported as NormalMap.
Textures, normal-map strength, material settings, facial hair and shadows were
not globally disabled as a fix.

The diagnostic images are under [Diagnosis](Diagnosis), with Original,
NoNormalMap, UnlitSkin, SkinOnly and PoseNormals variants for neutral, Joy,
left/right blink and FF. `diagnosis.json` records the comparison setup.

## Scoped correction

The current section manifest, not Ethan's offsets, identifies these ranges:

| Section | First vertex | Vertices | Triangles |
|---|---:|---:|---:|
| Main head | 0 | 20,175 | 37,630 |
| Split lip section .001 | 55,469 | 2,111 | 3,510 |
| Split lip section .002 | 57,580 | 2,134 | 3,512 |

For all 73 expressions, derive neutral and posed geometric normals from each
section's own topology. Rotate authored neutral normals/tangents by the change
in geometric normals. Preserve neutral shading and tangent handedness. All
sampled geometric normals are nondegenerate and the written values are finite.
Including both lip sections avoids the earlier Ethan omission that left dark
speech lips after correcting only the main head.

The correction changes expression shading only. Every source position record is
retained verbatim, as are all untouched shading records. Sparse indices are unique
and ascending within every frame. Two shading-only zero-position records were
added; frame offsets/counts were rebuilt while retaining channel definitions and
frame weights. The final source topology, UVs, colors, skin weights and bind poses
are unchanged. No blendshape was renamed, removed or rebaked.

## Verification and review

`verification.json` independently checks the reimported Unity API data against a
held pre-repair mesh. It passes exact comparisons for all 73 expression position
arrays, all base mesh attributes and neutral shading, topology, all eight UV
channels, skin influences, bind poses, expression names/order/frame weights and
all shading outside the three scoped sections. There are 1,778,332 changed scoped
shading records; body, brows/lashes, teeth, eyes and other overlay shading remain
bit-identical. The installer restores the exact backup automatically on a failed
guard.

[Previews](Previews) contains 34 matched before/after renders for 17 poses:
neutral, left/right/both blink, raised brows, all six emotions, FF/PP/aa/TH and
mixed speech/blink-smile poses. Both sides use identical geometry, materials,
camera and lighting, with expressions and skinning explicitly evaluated. The
before images use the actual preserved original shading, not an approximate
reconstruction. Neutral before/after pixels are exactly identical.

Joy, blink, FF, anger, surprise and mixed-speech results were visually inspected.
The jagged dark eye/nose/lip bands are removed while legitimate expression
creases remain. This is saved-mesh/static render verification, not runtime
acceptance of every animation/expression combination. Scalp simulation is omitted
from these face-focused images; existing user hair visibility is unchanged.

Final configuration checks pass for the consolidated hierarchy (exactly Face and
Outfit, no BAKED_ objects), lip-sync callback, modules and flow. The active scene
Face uses the corrected mesh. No new console errors appeared after cursor 413,
and no compilation failure was reported; existing console messages were retained.

## Counts and memory

Face remains **100,369 vertices / 175,832 triangles / 73 shapes**, with a final
loaded-prefab Unity mesh memory estimate of approximately **258.12 MiB**.
Brows remain **24,063 vertices / 38,568 triangles** and lashes remain
**11,190 vertices / 17,484 triangles**. Their geometry, skinning and expression
shading are unchanged. Dense position-only expression storage for these facial
hair sections remains **29.45 MiB**, or **58.90 MiB** including dense normal deltas;
these sections are inside Face, not additional separate renderer allocations.
No density or simulation setting was changed.

## Preservation and rollback

The exact target mesh and importer metadata are backed up as before_face.asset
and before_face.asset.meta in this directory. To roll back this repair, restore
before_face.asset to the target asset path and reimport through Unity. Metadata
and GUID are unchanged; restoring unrelated prefab/scene files is unnecessary.
Original Blender/FBX/DNA/grooms, existing materials, the candidate hierarchy,
voice, gaze, startup/readiness and animation data were not edited. CharacterTesting
was already dirty and was not saved. No Play Mode transition or build occurred.

User-controlled speaking/expression playback remains the final runtime review.
The separate Waving garment-fit issue remains outside this shading repair.

Tools under Tools/Liora: AuditShading.cs, DiagnoseExpressionShading.cs,
RepairExpressionShading.cs (Prepare, ExportBefore, Install),
preserve_shading_repair.py, ReviewExpressionShading.cs and FinishShadingRepair.cs.
References consulted: integrate-metahuman-character SKILL.md,
repair-character-shading SKILL.md and cases-and-preservation.md, using-unity,
unity-cli.md and its common-workflows.md/integration-advanced.md references.

Clothing follow-up: the separately noted garment breakthrough has since been repaired; see [ClothingFit](../ClothingFit/README.md).

