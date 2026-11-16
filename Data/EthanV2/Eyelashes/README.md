# Ethan V2 mesh eyelashes

Production override: the V2 prefab now uses the optimized head mesh described in
[FacialHairOptimization](../FacialHairOptimization/README.md). The dense FBX and
Blender sources documented below remain unchanged as authoring/recovery sources.

The active V2 FBX now has mesh eyelashes inside `BAKED_head_lod0_mesh`, alongside
the approved mesh eyebrows. Use `Ethan_V2_73_WithBrowsAndLashes.blend` as the
latest editable source. The previous FBX and importer metadata are in `Before`.

The modeller's `BAKED_Groom_Group_0` contains 542 lash strands. All strands are
retained with 32 samples and three-sided cross sections, adding 52,032 vertices.
The root diameter is 0.14 mm, tapering over the final 40% to 0.014 mm. Original
roots, tips, direction and length are retained. Barycentric skin-triangle
bindings translate and rotate strands for every one of the 73 facial shapes.
The initial root-to-surface correspondence error is below 0.001 mm.

The former `BAKED_eyelashes_lod0_mesh` lash-card renderer is excluded from FBX
to avoid doubling the lashes. It remains hidden in the editable Blender copy
for recovery. `Ethan_EyelashStrands.mat` is a solid dark material for geometry;
it does not use the old lash-card alpha texture. The head material order is
skin, eyebrows, lash strands. Existing brow material settings are preserved.

All 74 lash positions (Basis plus 73 shapes) passed independent FBX readback,
with maximum error 0.00108 mm. All 888 comparisons of retained mesh shapes,
including the entire approved eyebrow section, passed within 0.00168 mm.
See `fbx_verification.json`. Geometry previews include neutral, both individual
blinks and joy. These are clay previews; review the result under scene lighting.

The export contains 12 baked character mesh renderers. No hidden authoring
duplicates, scalp curves, or control widgets are exported. The scalp-hair setup
and approved eyebrow geometry are unchanged. The model GUID and previous
material remaps are retained. No scene is automatically saved, no Play Mode
transition is made, and no build is run. `unity_installation.json` records the
model import and the open-scene material check.

Generator: `Tools/EthanV2/export_mesh_eyelashes.py`, run on the approved
`Eyebrows/Ethan_V2_73_WithEyebrows.blend`. Independent readback:
`Tools/EthanV2/verify_mesh_eyelashes.py`. The source eyebrow `.blend` remains
unchanged. Future edits should start from the newer brows-and-lashes file.
