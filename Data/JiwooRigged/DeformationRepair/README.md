# Shoulder seams and facial groom surface binding

The user's recording showed open skin seams at the shoulders/collar and facial
grooms that did not deform with the face. These are separate from the earlier
Humanoid reference-pose correction.

## Skin seam repair

The delivered body and head have 92 matching boundary vertices. Their neutral
positions differed by only micrometers, but their independently authored Rigify
weights differed substantially (maximum L1 difference 1.31396). Animation could
therefore separate the two sides of the same seam.

The exporter now copies the body's boundary position, normalized influences,
and every shape-key boundary position to the matching head boundary. The source
Blender delivery and prepared review file are unchanged. The corrective export
changes no topology and retains all 73 facial shapes and 160 deform bones.

Unity's imported mesh now has zero seam position gap, zero weight discrepancy,
and zero shape-delta discrepancy at all 92 matching boundaries. Identical
positions and bone influences ensure that both sides receive the same skinning
transform, without needing an animation test to establish that invariant.

## Facial groom binding

`FacialGroomSurfaceBinding` binds all 1,915 eyebrow and 787 eyelash roots to
triangles on Jiwoo's skin. It samples the skinned, blendshaped face once per
runtime frame, after the expression module, and updates each root's position
and orientation. Authoring uses neutral skinned geometry to account for the
FBX vertex basis; it does not sample a clip.

Facial hair physics stays disabled. `FacialGroomKinematics.compute` reconstructs
the strands from their immutable initial offsets and surface frames, then the
existing hair package stages/renders them. This is necessary because changing
root meshes alone does not move frozen simulation particles. Runtime scheduling
is owned only while the component is active; private meshes/buffers are released
and original meshes/scheduling restored when it is disabled. Scalp hair and Ethan
are unchanged. Runtime work is guarded against Edit Mode and unsupported compute.

Both the prefab and saved scene contain the bindings. Rest reconstruction error
is zero; numeric rigid-transform covariance error is below 0.00027 mm. Root
offsets from the skin are below 0.0005 mm, so the authored placement is retained.

## Checks and handoff

- Blender FBX/ABC readback passed, including all shapes and bones.
- Unity seam/binding checks and compute-kernel compilation passed.
- The saved Humanoid T-pose remains valid with pose error zero.
- Liora, visemes, selection buttons and animation/flow references pass static
  checks. The 29 protected Ethan/shared assets remain unchanged.
- Unity compilation passed with zero compiler errors or warnings.
- No Play Mode changes, clip previews, animation/audio playback, GPU runtime
  dispatch tests, or builds were performed. Runtime appearance/performance still
  needs user confirmation.

Start Play Mode yourself, switch to Girl, and check shoulders/collar during
greeting and idle. Then check eyebrow/eyelash alignment while blinking, turning
the head, and speaking. Switch away/back to check reactivation. Facial binding
adds one CPU face-skinning readback per active frame; performance was not measured.

Reports: `weight_continuity_audit.json`, `surface_binding.json`, and
`verification.json`. The previous model/metadata and unsaved scene snapshot are
preserved here for review. Extracted frames are from the supplied recording.

Reproduction: run the corrected `export_jiwoo_rigged.py`, then
`verify_jiwoo_rigged_export.py`. In a saved Edit Mode scene, run
`RepairJiwooDeformation.ImportCorrectedMesh`, `BindFacialGrooms`, and `Verify`
through the Unity CLI script runner. Rebuild surface bindings after a model
revision that changes vertex ordering, topology, or groom placement.
