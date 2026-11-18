# Liora Face/Outfit hierarchy correction

18 November 2026. The initial candidate kept the bake-export mesh sections as
separate objects. Those sections had working synchronization, but did not deliver
the Face/Outfit layout requested for the project. This correction consolidates the
actual meshes and references; it is not only a rename.

## Current prefab

`Assets/_Sandbox/Characters/Liora/Candidate/Liora_Candidate.prefab` now has the
same four root children, in the same order, as the inspected current Ethan V2
candidate: **Armature, Outfit, Modules, Face**. Old Ethan and Jiwoo were inspected
as additional references; their renderer names also establish Face/Outfit.
No BAKED_ transforms remain in Liora's candidate. There are two skinned renderers.

Face merges 13 previously visible sections, including the body, eyes, teeth,
split mouth, adaptive brows and lashes. The primary head is first, with unchanged
vertex/triangle order and base attributes. Material sections and UV channels are
retained. Section names, source assets, offsets, counts, material paths, transforms
and skin-bone paths are recorded in sections.json.

| Mesh | Vertices | Triangles | Expressions | Unity mesh memory |
|---|---:|---:|---:|---:|
| Face | 100,369 | 175,832 | 73 | 258.12 MiB |
| Outfit | 5,196 | 9,029 | 73, preserved | 15.60 MiB |

Facial-hair geometry is unchanged: brows 24,063 vertices/38,568 triangles, lashes
11,190 vertices/17,484 triangles. Dense position blendshape storage for these
sections is 29.45 MiB total; including dense normal deltas gives 58.90 MiB. The
prior separate-mesh memory figures are not additional allocations on top of Face.
All facial-hair source positions, thickness/taper, skin influences and expression
deformations are retained through the section coordinate conversion.

## Reference changes and preservation

ExpressionModule.face and the lip-sync driver's renderer now reference Face.
The uLipSync persistent event now targets the driver on Face. Serialized character
references were remapped explicitly, with no stale references to removed objects.
The 14-target synchronization list was replaced with a single Outfit binding:
the garment's small nonzero expression data was not proven redundant, so it was
preserved rather than discarded. No separate facial sections need synchronization.

The Animator, native bones, calibrated Avatar, hair asset, fitted SDF, attachment
frame, voice profile/clip, gaze frame, modules, activities, flow and startup/
readiness settings are retained. Hair is still 32 points/strand at 30 Hz, Fixed
strand scale, with Edit Mode simulation disabled.

The exact previous prefab and metadata are preserved in [Before](Before). Existing
individual mesh assets were not edited or deleted and are no longer Face
renderer dependencies. Original Blender, FBX, DNA, textures, raw grooms and
other characters remain unchanged. The prefab metadata/GUID is unchanged.
No scene save, automatic scene replacement, Play Mode transition or build occurred.

To roll back this correction, restore Before/Liora_Candidate.prefab to the original
prefab asset path and reimport in Unity; its mesh dependencies remain intact.
Do not restore an entire unrelated scene over subsequent user edits.

## Verification

- All **219 dense position/normal/tangent expression arrays** on the final merged
  Face are bit-identical to the projected source arrays. Canonical sparse records
  preserve even tiny deltas and contain no staging tangent sentinels.
- All **962 source-section/pose comparisons**, including neutral and each of the
  73 expressions, passed. Maximum world-space difference is **0.000478 mm**.
- Primary head base positions/normals/tangents and triangle order are unchanged.
  Outfit uses the original mesh asset. Skin weights and per-renderer bind offsets
  are compensated for the merged coordinate frame.
- Character and flow configuration pass; one valid Humanoid Animator, no missing
  scripts, no stale character references, and the lip-sync callback points to Face.
- The scalp/collision assets are unchanged; the hair attachment matrix difference
  is zero. Startup/readiness and voice/gaze references remain internal.
- **40 Edit Mode samples across eight Humanoid clips**, plus all **30 finger
  mappings**, pass after consolidation.
- Nine updated [static previews](Previews) cover neutral, blink, brow raise, joy,
  speech, three-quarter, Waving, Talking and Thinking. Neutral and blink were
  visually checked against the earlier candidate review. The scalp remains the
  documented static one-fifth-strand proxy for these images; they do not prove
  runtime simulation quality or final strand shading.

Existing Waving garment breakthrough and full-strength Joy expression shading
remain as documented before this correction. Runtime hair settling, collisions,
voice playback and live gaze still require user-controlled Play Mode review.

Final console check reported no compilation failure and no new errors since
cursor 412; the two existing errors and warnings were not cleared.

Tools: MergeHierarchy.cs (Draft), preserve_merged_face.py, MergeHierarchy.cs
(Install), VerifyMergedHierarchy.cs (Static/Motion), RenderMergedCandidate.cs and
FinishHierarchy.cs. All are under Tools/Liora; Editor scripts compiled in memory.
Skills/references: integrate-metahuman-character SKILL.md, hierarchy-and-wiring.md,
bake-and-fbx.md, and the previously read Unity CLI workflow references. Historical
Ethan sparse-record and merge tools were inspected and parameterized for Liora.

Expression shading follow-up: the full-strength Joy dark shading noted above is now repaired. See [ShadingRepair](../ShadingRepair/README.md) for the scoped head/lip correction and matched before/after verification.

