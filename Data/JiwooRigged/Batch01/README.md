# Jiwoo Rigged — facial bake review checkpoint

This batch prepares facial shape keys only. Unity assets and the existing Jiwoo
setup have not been replaced or cleaned up. Do not use this file as the final
Unity export yet: export skeleton consolidation and integration follow review.

## Open and inspect

Open `Jiwoo_Rigged_Baked.blend` in Blender 5.2 or newer. Embedded scripts do not
need to be enabled. The original Downloads file is unchanged.

The visible `JIWOO_BAKED - review shape keys here` collection contains the prepared
meshes, outfit, fitted grooms, and independent neutral armatures. Original
authoring objects remain in the hidden `SOURCE_AUTHORING - preserved, hidden`
collection. Leave that collection hidden during review to avoid duplicates.

Select `BAKED_head_lod0_mesh`, open Object Data Properties > Shape Keys, and move
one shape from 0 to 1. Reset it to 0 before inspecting the next shape. Each mesh
has the same ordered names; for jaw/viseme review set the same shape on affected
teeth, saliva, tongue-containing head mesh, and other facial meshes as well.
The bake preserves separate meshes, material slots, and skin weights.

For convenience, this can be done from Blender's Python Console (replace the
name for each pose; use `None` to return every mesh to neutral):

```python
name = 'viseme_aa'
for obj in bpy.data.collections['JIWOO_BAKED - review shape keys here'].objects:
    if obj.type == 'MESH' and obj.data.shape_keys:
        for key in obj.data.shape_keys.key_blocks[1:]:
            key.value = float(key.name == name)
```

Do not pose the facial bones on top of their baked shape keys. Frozen review
armatures preserve neutral deformation and skin weights without requiring the
original control rig or DNA solver to run.

## What requires your judgment

- These are newly authored Rigify poses, not recovered animation clips or exact
  reconstructions of the older character's expressions. All non-neutral shapes
  require visual approval despite numeric bake checks.
- Inspect blink closure and eyelid/eyeball contact, mouth seals, jaw motion,
  tongue/teeth intersections, gaze direction, and the six combined emotions.
- `viseme_sil` is deliberately neutral. An unaffected mesh can have a neutral key
  for a shape that moves a different mesh; these retain the synchronized names.
- Eye-edge/cartilage meshes driven only by the older head rig do not deform with
  the new Rigify facial controls. The delivered rig also moves portions of the
  outfit by up to 5.05 mm during facial poses. Both require review; the bake
  retains the measured source behavior rather than inventing corrections.
- Facial groom surface deformation is not certified by the mesh bake. Review
  eyebrow and eyelash attachment separately; final Unity groom handling follows
  this checkpoint.
- The source's embedded rig UI and native DNA solver were not executed. Neutral
  fidelity is measured against the delivered file loaded with scripts disabled.

## Artifacts and reproducibility

- `shape_pose_mapping.json`: explicit control translations (rig-space meters) and
  rotations (radians) for each of the existing 73 names.
- `bake_report.json`: source hash, meshes, preserved weight hashes, groom counts,
  per-shape/per-mesh movement, and limitations.
- `evaluated_baselines.npz`: independently sampled rig-pose geometry used for
  saved-file comparison, outside Unity's Assets directory.
- `reopen_verification.json`: result of reopening the saved file and checking
  shape output, neutral restoration, vertex counts, names, and weight hashes.

Generator: `Tools/Hair/bake_jiwoo_rigged_shapes.py`. Run Blender in background mode
with `--factory-startup --disable-autoexec`, then the delivered source path and
`--python` with the generator path. It refuses to overwrite a review file. To
verify, open the prepared file with the same flags and script, adding
`-- --verify`. No Unity execution, Play Mode, or build is part of this batch.

Saved-file verification passed all 876 mesh/shape comparisons (12 meshes x 73
shapes), with maximum world-space error 0.00107 mm. Neutral restoration, vertex
counts, ordered names, weight hashes, and all three grooms' original control
points and world frames passed. The source SHA-256 remained unchanged. These
checks establish bake fidelity, not the artistic quality of the authored poses.

After your review, the next batch exports the approved shapes, replaces Jiwoo in
Unity, and then removes superseded Jiwoo-specific work while keeping the shared
character hair/readiness improvements.
