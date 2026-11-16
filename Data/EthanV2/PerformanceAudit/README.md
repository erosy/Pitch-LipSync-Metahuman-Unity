# Ethan V2 performance inspection - 16 November 2026

Read-only audit of the installed V2 prefab and the current loaded
`Ethan_V2_73_Baked` instance. Unity was in Edit Mode. No prefab/model/material,
scene, Play Mode state or build was changed. Findings are structural workload
comparisons, not measured runtime CPU/GPU frame timings.

## Main suspect: dense mesh eyebrows and eyelashes

| Enabled geometry when character root is active | Old Ethan | Ethan V2 |
| --- | ---: | ---: |
| Skinned vertices | 112,689 | 396,523 |
| Triangles | 164,940 | 748,415 |
| Skinned renderers | 2 | 12 |
| Primary head vertices | 103,825 | 341,443 |

Old Ethan's disabled alternative scalp meshes were excluded from these totals.
The loaded scene agrees with the saved V2 mesh and hair settings. Old Ethan and
Jiwoo are inactive; no duplicate active character was found in this inspection.

V2's head contains 38,307 skin triangles, **521,544 eyebrow triangles** and
**100,812 eyelash triangles**. The brows/lashes alone account for 622,356
triangles, approximately 83% of the character's enabled mesh triangles. The
source conversion notes report 269,184 added brow vertices plus 52,032 lash
vertices: 321,216 additional vertices, all carried in the 73-shape head mesh.
This is a fidelity-first 32-sample, three-sided tube conversion, not a small
facial-hair mesh. It increases skinning, blendshape processing and render work.
Having 73 shape names alone is not a regression: old Ethan also has 73.

## Scalp is already at production defaults

Both old Ethan and V2 use 94,408 scalp strands, 32 points per strand,
3,021,056 particles, 30 Hz, four substeps and twelve constraint iterations.
Both have a single full-guide LOD; neither has reduced-guide clustering.
V2 Edit Mode hair simulation is disabled. There is no 64-point/60 Hz regression.
The same warmup/reset behavior still reallocates hair runtime buffers on
activation/reset, so it remains a possible selection hitch, but does not
explain a V2-specific increase in strand count or cadence.

## Additional bake overhead

Unity reports approximately 432 MiB of mesh memory across V2 renderers,
including about 268 MiB for the head and 97 MiB for the body. This is Unity's
mesh-memory estimate, not measured GPU residency or frame time.

The body retains all 73 channels despite having zero nonzero positional and
normal deltas. Those channels contain 2,348,702 nonzero tangent-delta entries,
so they are not empty data from Unity's perspective. This deserves an export
cleanup investigation rather than deleting useful facial shapes. The outfit
has real position/normal deltas and must not be stripped indiscriminately.
Authored head/blink normals should be preserved.

Global skin weights are Unlimited. V2 mesh assets have up to eight bone
influences on the head/body and twelve on the repaired outfit. V2 also uses a
skeleton pose bridge and facial shape synchronization across secondary meshes.
These are additional costs to profile; no CPU garment-surface deformation
component is attached to the active V2 prefab.

## Next optimization

Prioritize a lower-cost mesh-brow/lash derivative, preserving original sources,
root attachments, endpoints, material appearance, all 73 expression names and
blink deformation. Reduce cross-section/curve tessellation or use an approved
card representation; do not lower scalp settings or remove expressions as a
substitute. Separately remove provably redundant body shape/tangent data after
checking the exporter and all facial-sync bindings.

A user-controlled Play Mode A/B capture should isolate facial-hair submeshes,
then scalp simulation/rendering, to establish the exact GPU/CPU contribution.
No runtime bottleneck or FPS gain is claimed solely from these static counts.

Raw data: `comparison.json` and `shape_density.json`. The Native README describes
the original solver bake; the installed FBX also includes the later mesh-brow
and mesh-lash conversions described in `../Eyebrows` and `../Eyelashes`.
