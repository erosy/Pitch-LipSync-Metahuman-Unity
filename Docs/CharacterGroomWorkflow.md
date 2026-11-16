# Character and groom production workflow

## Required hair defaults

For every future character, use **32 points per strand** and **30 Hz simulation**
unless the user explicitly requests an exception. Keep Edit Mode simulation off.
Jiwoo's approved 32-point/30 Hz scalp is the current integration reference.

Preserve the original modeller delivery and work in separate prepared sources
outside Unity Assets. Deliver an FBX, head-local scalp Alembic, textures, editable
Blender source and front/side/back references from the same model revision.
Document units, skeleton/head bone, shape names, material order, strand count,
widths, bounds and coordinate conversion. Keep source scripts/reports outside
Assets; only import assets needed by Unity.

## Prepare and export

1. Use neutral geometry/expressions and an explicit export skeleton. Preserve
   weights, shape keys, UVs, materials and existing authored normals. Exclude
   authoring controls and duplicate meshes from the export selection.
2. Use meters with Blender scene unit scale 1. Confirm the character dimensions
   and imported root/head world scales; do not compensate with hair-object scale.
3. Export scalp curves relative to the current export head, ordered root to tip.
   Sample 32 points per strand and preserve both endpoints. Retain a denser editable
   groom separately. Check silhouette/curvature after resampling.
4. Supply valid positive strand widths in meters. Blender curve radius is half
   the diameter. Jiwoo uses 0.1 mm diameter; choose appropriate artistic widths
   for other models rather than changing scale to hide a width error.
5. Verify independent FBX/Alembic readback, including dimensions, root location,
   endpoint order, point count, widths and handedness. Check the Unity-imported
   data too; Blender and Unity head-local axes need not match directly.

The existing Blender FBX profile uses selection-only Armature/Mesh export,
scale 1, Apply Unit, FBX Units Scale, -Z Forward/Y Up, Use Space Transform,
Apply Transform off, Apply Modifiers off, Y/X primary/secondary bone axes,
no leaf bones and no animation bake for the neutral delivery. Preserve the
project's verified Humanoid mapping and reference T-pose. Validate these settings
against each new delivery rather than assuming an older groom still fits.

## Integrate in Unity

Mesh eyebrows/lashes have a separate budget from simulated scalp strands. Use
adaptive low-poly tubes (typically 3–12 axial rings) or approved cards instead of
copying 32 scalp samples into every facial-hair tube. Preserve thickness/taper,
roots/tips and all facial shape bindings. Check about 0.05–0.1 mm curve tolerance
and neutral/blink/raised-brow views. Report their vertex/triangle and blendshape
memory costs; a dense 73-shape facial-hair bake needs optimization before shipping.

- Back up the current prefab/groom before changing them. Preserve prefab GUIDs,
  model bindings, avatar, clothing/facial repairs, activities, expressions and
  lip-sync references. Install hair separately from character replacement.
- Attach one scalp Hair Instance to the current head bone with zero local
  position/rotation and unit scale. Use Fixed strand scale and meter-based data.
- Build the production Hair Asset with 32 points per strand. Set simulation to
  Fixed30Hz and disable simulation in Edit Mode. Do not silently increase the
  point count or simulation rate for a new model.
- Use the character's strand material. Keep mesh eyebrows/lashes where supplied;
  do not duplicate them with Alembic facial grooms.
- Fit the head boundary to the actual current scalp, including temples, crown
  and back of skull. An undersized generic capsule can leave visible penetration.
  Add appropriate neck/shoulder boundaries following their skeleton bones.
- Wire `CharacterHairStartupWarmup` and `CharacterReadiness` as described in
  [CharacterHairSupport.md](CharacterHairSupport.md).
- Preserve compute-support guards. Supply a matching character-specific card/mesh
  fallback before deployment to unsupported platforms.

## Verify and review

Check saved-prefab references, identity attachment, Fixed scale, point count,
30 Hz rate, widths, material, collision fit, startup wiring and missing scripts.
Compile and run relevant Edit Mode checks. Do not save an already-dirty scene.
Do not automatically enter/exit Play Mode or run a build.

The user reviews selection/startup, greeting, idle, head turns, blinks, hairline
stability, clipping, settling and frame time in Play Mode. Changing sampling
can affect curl detail and collision between adjacent particles. Exact FPS and
GPU/CPU cost require runtime profiling. A high strand count without reduced
simulation guide LODs can remain expensive even at 32 points/30 Hz.

Current assets and source locations: [JiwooRigged.md](JiwooRigged.md).
