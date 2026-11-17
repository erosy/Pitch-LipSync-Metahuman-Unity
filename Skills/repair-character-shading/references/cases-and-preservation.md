# Cases and exact preservation

Project-relative evidence; inspect scripts before adapting them. They contain
character-specific paths and destructive installation steps, not generic commands.

| Case | Evidence and resolution | Project sources |
| --- | --- | --- |
| Jiwoo black blink folds | Normal texture and overlay removal did not help; posed normals did. Authored FBX blendshape normals corrected it with base normals still Import. | `Data/JiwooRigged/BlinkAudit/README.md` (its historical `Tools/Hair` scripts are not present in this checkout) |
| Ethan head expression shading | Incorrect normal/tangent deltas. Importer-only trial changed welding/position records and was reverted. Derive shading from the production mesh's own topology. | `Data/EthanV2/NormalRepair/README.md`; `Tools/EthanV2/RebuildDeformationNormals.cs` |
| Ethan runtime face breakup after correction | 34/73 sparse frames had out-of-order indices. Preserve full records but sort strictly by index within each frame and reimport buffers. Initial static renders did not prove runtime correctness. | `Data/EthanV2/SmileRepair/README.md`; `canonicalize_shape_records.py`, `InstallCanonicalFace.cs` |
| Ethan dark speech lips | Main-head correction omitted split lip meshes `.001`/`.002`. Correct only their expression shading in the merged candidate. | `Data/EthanV2/LipNormalRepair/README.md`; `RepairCandidateLipNormals.cs`, `preserve_lip_normal_records.py` |

For topology-derived expression shading, compute geometric neutral normals `g0`
and posed normals `g1` from the same triangles. Rotate authored neutral shading by
`Q = FromToRotation(g0, g1)`, then store `dn = Q*n0 - n0` and
`dt = Q*t0 - t0`. Keep tangent handedness, neutral normals and tangent basis.
Check finite values, degenerate vertices and seams. This method was effective for
Ethan; verify visual quality rather than assuming it is right for every topology.
For isolated sections, preserve their own topology and correspondence.

Do not use `AddBlendShapeFrame` reconstruction as proof of exact position
preservation: it dropped tiny existing position-only entries in the head case.
Use a sparse-record-preserving staged transformation when needed:

- Retain every original position record and all untouched shading records.
- Replace only scoped normal/tangent fields; add zero-position shading-only
  records where necessary. Keep unique, ascending vertex indices per frame.
- Recompute frame offsets/counts, preserving channel definitions/frame weights.
- Keep edits staged outside Assets; import only the final intended asset through
  Unity and compare dense arrays and base attributes against a held baseline.
- Preserve asset GUID/reference identity and refresh buffers. Restore the backed-up
  target on failed validation, without reverting later unrelated prefab edits.

Historical Ethan lip scope: 4,252 vertices / 7,142 triangles, indices
139924–144175 in a 149,611-vertex merged Face. All 73 expression positions and
non-lip shading were exact after correction; one shading-only sparse record was
added. This identifies that particular artifact, not a reusable index contract.

Matched renders should include full facial overlays/materials as well as isolated
skin diagnostics. Neutral paired pixels can prove neutral preservation; small
average image differences do not prove eyelid contact, absence of jagged marks or
correct combinations. Inspect the artifact region and state remaining uncertainty.
