# Native solver bake and FBX

## Establish the input

Inventory Blender version, DNA extension/version, character DNA identity, source
meshes, armatures, face board, corrective data, grooms and current project shape
contract. Ethan's contract is 73 ordered names, recorded in
`Data/EthanV2/shape_names.json`; discover the target contract rather than assuming
all characters have those names. Record source hashes and vertex/material counts.

Brows/lashes may originate as separate DNA/Alembic-associated objects while
already following the MetaHuman facial rig in the delivered `.blend`. Inspect
their actual modifiers, attachment, drivers and evaluated motion before proposing
new rigging. Separate storage is not evidence of a missing facial binding. Keep
the working `.blend` and its external DNA/Alembic dependencies together.

Use `Data/EthanV2/Native/README.md`, `Tools/EthanV2/reconnect_solver.py`,
`restore_native_correctives.py`, `native_pose_mapping.py` and
`bake_native_shapes.py` as worked examples. These scripts contain source paths,
names, addon APIs and tolerances specific to Ethan: inspect and parameterize them
in a new tool; do not run them blindly against another model or overwrite a bake.

## Bake the evaluated result

1. Restore/initialize the original DNA/RigLogic evaluator and its native
   correction shapes. The approximate Rigify face is not an equivalent source.
   For reindexed/split meshes, establish neutral vertex correspondence and report
   maximum match error; reuse old indices only when ordering is proven identical.
2. Reset every face-board control to neutral before each sample. Map the project's
   expression names to native controls/presets, saving controls and provenance.
   Evaluate native facial bone motion and correction deltas together.
3. Invert the neutral skinning stack when storing mesh-local shape positions so
   the evaluated bake is not skinned twice. Preserve neutral armatures/weights
   while keeping native facial pose evaluation frozen in the exported bake.
4. Bake all participating meshes, including eyes, separate lip sections, teeth,
   tongue, brows, lashes, cartilage and saliva. Make expression names/order consistent;
   retain synchronization/drivers where meshes remain separate. Do not animate
   frozen facial bones on top of the same baked facial poses.
5. Reopen the saved baked file with addon evaluation disabled and independently
   compare neutral and every shape against stored evaluated solver samples in
   world-space meters. Verify neutral restoration, skin weights, material slots,
   curve points/world frames and the saved file, not only the in-memory bake.

For already-rigged brows/lashes, sample their evaluated result with the face for
neutral, closed blink, squint, brow raise and relevant combinations. When the
runtime route replaces facial rig evaluation with blendshapes, bake their matching
deformation too; do not assume a Blender-only attachment/driver or Alembic origin
will transfer automatically through FBX. If converting curves to production meshes,
preserve that evaluated motion and validate the conversion against the source.
Retain separate exported meshes with synchronized expression weights, or optionally
merge the baked geometry into Face. Merging alone does not create deformation.
Verify eyelid contact and brow motion again after Unity import. A valid existing
binding does not require the modeller to re-rig or merge the authoring objects.

Ethan-specific semantics worth checking when adopting its contract:
`viseme_sil` is intentionally neutral; `mouthClose` is jaw-open plus lip seal minus
jaw-open, intended to combine with `jawOpen`. Tongue visemes may need calibrated
jaw opening; preserve documented semantics rather than copying old vertex deltas.

## Export/import and skeleton

Export only intended deform geometry/rigs; keep dense authoring/control objects
outside the active production asset. Record Blender units and export axes/scale.
Measure the imported character and its bind-space skin result. A 100x mesh
transform may be a legitimate conversion; multiplying scale fields until the
viewport looks correct can break bind poses, culling and grooms.

Inspect the actual `ModelImporter` and approved reference FBX for normals,
blendshape normals, tangents, material remaps, animation import and Humanoid setup.
There is no universally correct Import/Calculate blendshape-normal setting:
Jiwoo accepted authored normals; Ethan's importer trial changed vertex welding.
Verify geometry/order before adopting an importer change.

For the native candidate architecture, use one deform armature and one Animator
with a valid, calibrated meter-based Humanoid Avatar. Preserve all weighted facial
and helper bones. Calibrate a disposable T-pose representation for Avatar creation
without changing the authored neutral pose. Verify fingers and all Humanoid clips.
The older `AnimationDriver` + `CharacterSkeletonPoseBridge` + separate skin rigs is
a compatibility route, not the preferred template for a fresh native candidate.

When consolidating rigs, preserve each renderer's original skin transform:

`newBind = newBone.worldToLocal * oldBone.localToWorld * oldBind * inverse(X)`

Here `X` converts old mesh-local positions into the new mesh-local frame. Retain
per-renderer bind offsets even when two bones share a name. Merge weights with
explicit old-to-new index maps; never guess anatomical roles by proximity alone.

For the required final Unity Face consolidation, put the already-corrected head
first, retaining its vertex order and expression records so scalp attachment
remains valid. Keep a section manifest
with names, offsets, counts, transforms and materials for later lip/eye repairs.
Preserve every source position delta: Unity `AddBlendShapeFrame` can drop tiny
position-only records. Stage sparse records with strictly ascending indices per
frame and compare dense API arrays after import. Read
`Tools/EthanV2/preserve_candidate_head.py` and the SmileRepair report first.
Remove body tangent/shape data only after proving it inactive and checking facial
bindings and animation curves; duplicated expression names alone are insufficient.
