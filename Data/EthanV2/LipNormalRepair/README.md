# Candidate Ethan V2 lip shading repair — 16 November 2026

Installed in `Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Face.asset`.
The current production Ethan V2 prefab and original FBX remain unchanged.

The merged Face contains the separate `BAKED_head_lod0_mesh.001` and `.002`
lip sections at vertices 139924–144175: 4,252 vertices / 7,142 triangles.
Their expression normal/tangent deltas were inconsistent with their deformation.
For each of the 73 expressions, the repair computes the rotation between the
neutral and posed geometric normals and applies it to the authored neutral
normal and tangent. It changes only these sections' expression shading.

`verification.json` records the independent Unity API comparison after import:

- All 73 position arrays, names/order/frame weights are exact.
- Neutral positions, normals/tangents, topology, UVs, skin weights and bind poses
  are exact; all non-lip expression shading is exact, including the head repair.
- 310,396 lip shading records changed. Sparse indices are sorted per frame and
  every original position record is preserved. One shading-only record was added.
- Face remains 149,611 vertices / 257,691 triangles / 73 expressions.
- Facial hair remains 84,405 vertices / 148,734 triangles.
- Unity reports 162.59 MiB for the merged Face mesh after import. Sparse shape
  payload is 3,230,422 records × 40 bytes = approximately 123.23 MiB.

`Before` and `After` contain matching static evaluated full-character renders:
neutral, AA, FF, O, speech with smile, joy, blink and raised brow. Neutral pixels
are identical. The jagged lip shading is reduced in the speech/smile comparisons.
`comparison.png` has before on the left and after on the right, in order AA,
FF, speech, joy. Scalp simulation is not rendered in these previews.

Existing Edit Mode motion validation passed 63 samples across seven clips,
30 finger mappings, eight phoneme bindings and the lip-sync callback. See the
copied `motion_verification.json`. Repair and render tools compiled without
diagnostics. No scene save, Play Mode transition or build occurred. The loaded
CharacterTesting candidate uses the corrected asset. Gaze settings were untouched.
User-controlled speech playback is still required for runtime visual acceptance.

Reproduction: `RepairCandidateLipNormals.Prepare` creates a non-overwriting
backup and dense lip shading; `preserve_lip_normal_records.py` stages the asset;
`RepairCandidateLipNormals.Install` imports it, validates exact preservation and
rolls back automatically if validation fails. `RenderLipRepair.Run` renders the
installed result. The baseline backup and staging files are retained here for
recovery; restoring must preserve the active asset's `.meta` GUID and account
for any subsequent changes.
