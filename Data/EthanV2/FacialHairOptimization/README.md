# Ethan V2 production mesh facial hair - 16 November 2026

## Installed result

Later shading-only correction: [expression normal repair](../NormalRepair/README.md).
Topology and all expression position deltas remain exact; the installed normal
and tangent deltas now follow geometric deformation. Head memory is 118.86 MiB.

The saved V2 prefab now assigns
`Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_FacialHairOptimized.asset`
to its existing primary head renderer. The scene instance inherits it. The
already-dirty scene was not saved. No Play Mode transition or build was run.

| Mesh workload | Before | After |
| --- | ---: | ---: |
| Head vertices | 341,443 | 104,632 |
| Head triangles | 660,663 | 187,041 |
| Brow/lash vertices | 321,216 | 84,405 |
| Brow/lash triangles | 622,356 | 148,734 |
| Whole character vertices | 396,523 | 159,712 |
| Whole character triangles | 748,415 | 274,793 |

Facial-hair triangles decrease 76%; whole-character mesh triangles decrease
63%. These totals exclude procedural scalp rendering. Unity reports about
119 MiB for the active head mesh versus about 268 MiB for the dense head. This
is an individual mesh-memory estimate, not measured total RAM/VRAM or FPS;
the original FBX remains available and other meshes still depend on that FBX.

All 2,804 brow and 542 lash strands remain. The optimized topology retains
curvature-adaptive rings from the existing mesh: 5-12 rings for brows, 3-9 for
lashes, instead of 32 everywhere. Cross sections remain three-sided. Root,
tip and taper transition rings remain present; no strands were thinned and
materials/thickness were not changed. Maximum neutral centerline approximation
error is 0.0661 mm in character world space. Tighter curves retain more rings.

## Preservation and checks

- Skin triangles and their vertex attributes are retained, with reordered
  indices only; all retained base positions are exact.
- All 73 shape names/order/frame data remain. Retained position, normal and
  tangent deltas compare exactly against the source, including both blinks.
- Bind poses, retained per-vertex bone influences/weights, UVs and materials
  are preserved. No authored normal recalculation or FBX reimport occurred.
- Component snapshots before/after mesh assignment differ only by the primary
  renderer's mesh reference. Avatar, skeleton bridge, lip-sync, expressions,
  flow, clothing and accessories remain wired.
- Existing V2 checks passed: 657 secondary facial-sync checks, eight phonemes,
  lip-sync callback, neutral skinning, Waving, Breathing Idle, Idle and Thinking
  sampling. These are disposable Edit Mode checks, not runtime playback.
- Neutral, left blink, right blink and raised-brow closeups are under `Previews`.
  These isolate the primary head mesh; separate eye/mouth meshes and scalp are
  omitted. Paired captures use the same camera/light/materials and show similar
  coverage. They are geometry/shading comparisons, not full-character approval.
- Protected dependency hashes cover the original FBX, materials, scalp and
  collision assets, code dependencies and prefab metadata. They are unchanged.

Scalp hair remains 94,408 strands, 32 points per strand, 30 Hz, with its prior
solver/collision/startup settings. Body/outfit shape data was not stripped;
redundant body tangent data is a separate follow-up if profiling warrants it.
The user's Play Mode check should compare selection hitch and steady FPS, then
blinks, raised brows, speech and head turns at normal and close-up distances.
Reduced geometry is verified; actual FPS improvement has not been measured.

## Maintenance and recovery

The original dense head stays in `Ethan_V2_73_Baked.fbx`; editable Blender sources
remain unchanged. Restore that mesh reference on the existing primary renderer
to compare/revert, or use `Before/Ethan_V2.prefab` after preserving later edits.
`protected_hashes.json`, `build.json`, `geometry_verification.json`,
`final_check.json` and `installation.json` record this pass. Retained ring and
vertex mappings record the exact derivative selection. Source topology was
recovered from connected tube components because Unity reordered the vertices.

Future mesh brows/lashes use adaptive low-poly tessellation rather than blindly
copying the simulated scalp's 32-point setting. The production guideline is
3-12 rings with roughly 0.05-0.1 mm curve tolerance and visual deformation checks;
report vertex/triangle/blendshape cost before delivery. This rule is recorded in
root `AGENTS.md` and `Docs/CharacterGroomWorkflow.md`.

The one-off builder and source geometry dumps are preserved in
`authoring_recovery.zip`; no temporary builder/compute code remains under Assets.
