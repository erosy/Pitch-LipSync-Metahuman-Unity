# Clothing weight repair: greeting animation

Applied to the existing Jiwoo_EthanSetup FBX and Jiwoo_Normalized prefab. This
is step two after the seam repair: garment weights are transferred from nearby
body-surface triangles, normalized, and averaged consistently over the existing
489 seam clusters. The sleeveless torso garment retains spine/neck, shoulders,
upper arms, breasts and pelvis influences; jaw, ear, facial, forearm and hand
influences are excluded. Geometry, garment clearance, UVs, material slots,
all 73 shape positions, and facial skin weights are unchanged.

Five weight candidates were compared across 21 samples of the actual 4.7333 s
Waving clip referenced by the Greeting activity. Sampling uses a manually
evaluated PlayableGraph on a disposable model in Edit Mode. The best candidate
uses surface-transferred weights with seam-cluster averaging. Additional
whole-garment Laplacian smoothing was tested but produced a slightly worse
intersection score, so it was not applied to the installed candidate. The
body's transferred weight transitions provide the smoothness in this pass.

Independent FBX readback passed: unchanged topology, UVs, material names and
shape coordinates; maximum outfit weight roundtrip error below 0.000000003.
The current FBX and prefab GUIDs, calibrated Humanoid mapping/reference pose,
material remaps and character animation/expression/lip-sync references remain.
Outfit bone arrays were rebound by name in the saved prefab and current scene
to accommodate newly used torso bones. No live character pose was sampled or
changed, and Play Mode was not entered or exited. The open scene remains dirty.

Post-import sampling in Unity confirms:

- Same 21 greeting poses and identical body geometry before/after.
- Aggregate nearest-body-surface penetration-depth diagnostic reduced by
  **15.46%**, from 73.6424 to 62.2585 metre-sum across vertex/frame probes.
- Vertex/frame probes more than 1 mm inside the skin decreased from 12,619 to
  10,935. These are repeated vertex probes, not visible holes or affected pixels.
- All 524 matching seam pairs retain equal weights. Independent 20-degree
  shoulder/chest tests have zero pairs opening over 1 mm, with maximum gap
  still below 0.1 mm.

These probes compare nearest skin surfaces and interpolated normals. They are
diagnostic measurements, not full collision detection or certification that
every visible patch is eliminated. Remaining intersections indicate that
garment fit/clearance, garment topology, and/or masking body triangles covered
by this outfit may be needed after user review. This pass does not alter the
body, hide skin, add cloth simulation, or change material culling.

The latest editable source is `Jiwoo_EthanSetup_WeightsFixed.blend` in this
folder. Original modeller delivery and the prior seam-fixed Blender copy are
unchanged. `Before` preserves the previous Unity FBX, importer and prefab.

Evidence: `weight_comparison.json`, `repair_report.json`,
`blender_readback.json`, `unity_install.json`, `greeting_verification.json`,
`unity_stress_after.json`, and `prefab_verification_command.json`. Before/after
Unity geometry snapshots and metadata are retained here and under `After`.
Clay previews illustrate one sampled back view; they use simplified shading.

Tools: `SampleJiwooClothingGreeting.cs` (Main/AfterRepair),
`repair_jiwoo_clothing_weights.py`, `verify_jiwoo_clothing_seams.py` with
`--repair-root Data/JiwooRigged/ClothingWeightRepair`,
`InstallJiwooClothingSeams.WeightRepair`, and
`verify_jiwoo_clothing_greeting.py`. Run Blender with factory startup and scripts
disabled. The generator refuses to overwrite an existing artist candidate.
