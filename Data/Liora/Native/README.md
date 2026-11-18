# Liora native facial blendshape bake

Open [Liora_73_Baked.blend](Liora_73_Baked.blend) in Blender 5.2. Select
`BAKED_head_lod0_mesh`, then Object Data Properties > Shape Keys. Set an
expression from 0 to 1 and reset it before trying another. The other 12 mesh
sections follow the head sliders. Basis is additional to the 73 expressions.
Do not enable the hidden SOURCE_AUTHORING collection or animate frozen facial
bones over the baked expressions.

The 73 ordered project names match Ethan's contract: 52 facial motions,
15 visemes, and six emotions. They were sampled from Liora's original native
Polyhammer RigLogic, with DNA correctives restored onto her edited/split meshes.
They are not copied geometry from another character. Native facial bone motion
and corrective deformation were baked together through the inverse neutral
skinning stack. Weights, material slots, mesh topology and source grooms remain
preserved. `viseme_sil` is neutral. `mouthClose` is an additive lip seal to combine
with `jawOpen`; phoneme and emotion mapping provenance is in shape_pose_mapping.json.

## Verification and limits

Independent reopening with factory settings and auto-execution disabled passed
all 962 neutral/expression mesh comparisons, ordered names, weights, neutral
restoration, and groom control-point/world-frame preservation. Maximum saved-file
mesh error: **0.0004774 mm**. See reopen_verification.json.

Edited head correspondence to original DNA has a maximum nearest-neutral match
distance of **0.27123 mm**; teeth correspondence is **0.04946 mm**. This matching
uncertainty is separate from the saved-bake fidelity above. Review split inner
lips and tongue. See corrective_restoration.json.

Curve grooms retain the delivered surface deformation instead of being converted
to dense tubes. Seven neutral/blink/squint/brow/joy poses were compared to the
native evaluated curves. Most comparisons are below 0.001 mm; Group_0_001 reaches
0.1102 mm for browOuterUpLeft and 0.16784 mm for emo_joy. Therefore the strict
0.002 mm curve comparison did **not** pass. These differences remain a visual
review item; mesh blendshape validation passed independently.

Existing eyelash card mesh: **2,144 vertices, 1,722 triangles**, with 1,878,144 bytes
(1.79 MiB) of dense position data for 73 expressions, excluding Basis, normals
and Blender overhead. Curve brows/lashes were preserved as curves; no new tubular
facial-hair mesh or its blendshape allocation was created. Per-mesh counts are
in groom_motion_verification.json. Dense source authoring stays in the hidden
archive, outside the displayed baked collection.

Clay previews are in [Previews](Previews). Neutral and left blink were visually
inspected. Workbench clay rendering makes the source transparent eye/lash cards
opaque; these images assess geometry, not final textured shading. Numerical
verification and static previews do not establish runtime acceptance or combined
expression quality.

## Preserved sources and scope

The Downloads blend, textures, DNA and ABC grooms were only read. Source hashes
and a subsequent unchanged-file check are in source_manifest.json and
source_preservation_check.json. Packed textures remain packed; resolvable external
image paths were repaired in the baked candidate. Missing source topology/helper
images are recorded in the manifest. Original DNA is copied into Dependencies.

Liora_FaceBoard_Restored.blend retains the reconnected original face board and
correctives for authoring, requiring the installed Character DNA add-on.
Liora_Reconnected.blend is the earlier migration checkpoint. Keep Dependencies
next to the restored file. Reproducible scripts are under Tools/Liora.

This delivery is the requested Blender facial bake. No active Unity prefab,
scene, FBX, simulation, or readiness wiring was changed. No Play Mode transition
or build was run. A later Unity import needs skeleton consolidation and expression
synchronization; any scalp simulation must use 32 points/strand and 30 Hz.

## Humanoid import repair - 18 November 2026

The user's full-scene FBX included both the bake and hidden source archive:
26 skinned mesh sections, 7,651 transforms, repeated skeleton names, and an empty
Humanoid mapping. The imported Avatar was not Humanoid.

The project asset `Assets/_Sandbox/Characters/Liora/Liora_73_Baked.fbx` now uses
the clean export `Liora_73_Clean.fbx` with 13 baked mesh sections and 1,940
transforms. Secondary rig bone names have Face_/Rigify_ prefixes to make native
body mapping unambiguous. The native pelvis maps to Hips; 52 human bones are
explicitly mapped, including fingers. Unity 6000.5.9f1 reports Avatar.isValid=true
and Avatar.isHuman=true. All 13 sections retain 73 blendshapes. Bone renaming
introduced zero neutral world-space mesh error in Blender. The asset GUID remains
unchanged. Export/import tools and reports are under Tools/Liora and this folder.

Exact pre-repair FBX and importer metadata are in BeforeHumanoidFix. To restore,
copy both files back to the project asset path and reimport. The source Blender
file and Data/Liora/Native/Liora_73_Baked.fbx remain untouched. The active dirty
scene was not saved. No Play Mode transition or build was performed.

This resolves the Humanoid importer error. Separate frozen face and Rigify
clothing armatures remain; animation-ready integration still requires skeleton
consolidation and synchronized facial weights. Curve grooms are preserved in the
Blender source and are not part of this mesh/armature-only FBX. Importer Avatar
validity does not establish full-body animation or runtime acceptance.

The live Editor command timed out at 30 seconds while two FBX imports completed
(approximately 26 and 25 seconds); the operation completed successfully and was
checked again afterward. The console retains earlier messages plus diagnostic
and timeout errors from this session; it was not cleared. No compilation failure
was reported. Skill references consulted: unity-cli.md,
references/integration-advanced.md, references/common-workflows.md, and the
character integration bake-and-fbx.md.
