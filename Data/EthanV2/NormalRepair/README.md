# Ethan V2 expression shading correction — 16 November 2026

Follow-up: [expression record ordering repair](../SmileRepair/README.md).
The initial delivery's sparse records were out of order in 34 frames. The
follow-up corrects their ordering and rebuilds Unity's mesh buffers. The initial
static previews did not establish full-character runtime correctness.

The optimized production head had incorrect expression normal/tangent deltas.
Full blinks produced jagged dark patches below the eyelids, similar to the
earlier Jiwoo problem. Neutral geometry was not the cause.

The installed correction derives a normal rotation from each vertex's neutral
and posed geometric normals, then applies that rotation to its authored neutral
normal and tangent. This preserves neutral shading and follows the existing
deformation without transferring vertices between different imported meshes.
The saved prefab and open scene retain the same optimized mesh asset reference.

## Preservation and validation

- SHA-256 geometry checks cover every base position, triangle index, UV channel,
  bind pose, skin influence/weight, shape name/order/frame weight, and all 73
  expression position arrays. The installed result matches exactly.
- Neutral normals and tangents match exactly; paired neutral preview pixels are
  identical. Blink and raised-brow comparisons are in `SkinPreviews` and
  `Previews`. These isolate the primary head, not the complete character;
  detached preview hair shadows and omitted eye/mouth overlays are not a
  full-character appearance approval.
- Existing Edit Mode checks passed: 657 secondary facial-sync checks, eight
  phonemes, lip-sync callback, Humanoid avatar, neutral skinning, Waving,
  Breathing Idle, Idle and Thinking sampling.
- All 100 previously protected dependencies match their stored hashes,
  including source FBX/importer metadata, materials, scalp/collision and code.
- Final saved-prefab and live-scene mesh references match. No scene save,
  Play Mode transition, build, or runtime performance measurement occurred.
  Unity reports no compilation failure.

| Production workload | Before | After |
| --- | ---: | ---: |
| Head vertices / triangles | 104,632 / 187,041 | unchanged |
| Brow/lash vertices / triangles | 84,405 / 148,734 | unchanged |
| Expressions | 73 | 73 |
| Unity head mesh memory | 118.60 MiB | 118.86 MiB |
| Sparse shape vertex records | 2,306,025 | 2,313,122 |
| Estimated shape vertex payload (40 bytes/record) | 87.97 MiB | 88.24 MiB |

Scalp remains 32 points per strand and 30 Hz; facial-hair density and topology
are unchanged. Extra sparse records hold shading changes on adjacent vertices.

## Why the importer-only fix was not used

Jiwoo's solution was Blend Shape Normals: Import. With Ethan's FBX this changes
vertex welding and some expression position records. The importer trial was
reverted; its metadata is byte-identical to the initial state. A proposed
position-only transfer was rejected by automatic review and was not executed.
The final correction uses the production mesh's own indices and topology.

Unity's `AddBlendShapeFrame` also drops certain tiny position records when their
new shading deltas are zero. The initial review draft lost 336 such entries,
with maximum displacement 0.0714 mm in character world space. That draft was
not installed. The final stage retains every original sparse position record,
replaces shading fields, and adds shading-only records as needed. Exact geometry
checks pass with zero mismatches.

## Recovery and reproduction

`recovery.zip` holds `before_head.asset`, original importer metadata and hash
baseline, plus the staged dense shading arrays. Restore the old head asset
through Unity's asset import while preserving its existing `.meta`/GUID if a
rollback is needed; first preserve any later changes.

Tools in `Tools/EthanV2`:

1. `RebuildDeformationNormals.Draft` creates a separate review mesh from the
   unchanged original production asset. `ExportShading` writes the dense arrays.
2. Extract `before_head.asset` from recovery beside the baseline files and run
   `preserve_normal_shape_records.py` to retain the original sparse positions.
3. `RebuildDeformationNormals.ImportReview` imports the staged review asset.
4. `RepairEthanExpressionNormals.Diagnose` and `Install` require exact geometry
   hashes and neutral shading preservation before applying it in place.
5. `Finish` verifies production references and removes the temporary review mesh.

Do not rebuild from the already-corrected asset as a new baseline or use an old
source vertex map after changing importer topology. Review paired renders before
installation. The temporary review asset was removed after delivery.
