# Jiwoo normalized exports: Unity import instructions

For the reusable modeller-to-Unity workflow, delivery requirements, and subsequent
hair setup batches, see [Character and groom workflow](CharacterGroomWorkflow.md).

## Verified handoff

Import files from:

`D:/UnityProject/MetahumanTemplate/Data/JiwooReimport/Batch03/UnityImport`

- `Jiwoo_Normalized.fbx`: character, rig, skinning, material slots, facial shapes.
- `Jiwoo_Bob_HeadLocal.abc`: head-relative, meter-space groom, 100,493 strands.
- `Textures/`: 15 unique texture files extracted from the prepared source.

The original Blender file, prepared Blender copy, and current Unity Assets were
not changed during export/verification. Test imports used a separate temporary
Unity project with Unity 6000.5.9f1 and Alembic 2.4.7, matching this project.

## Step 4A — Copy the exports into the Project window

1. In Unity's Project window, create
   `Assets/_Sandbox/Characters/JiwooNormalized/Source`.
2. Drag `Jiwoo_Normalized.fbx` and `Jiwoo_Bob_HeadLocal.abc` from the handoff
   folder into this new folder. Keep the existing Jiwoo assets for comparison.
3. The exported `Textures` folder is available if you want separate new materials.
   For this checkpoint, reuse Jiwoo's existing materials; importing duplicate
   textures is optional.
4. Import only the export files, rather than the whole Batch03 directory. The
   parent directory contains numeric verification data and scripts, which are
   not Unity production assets. Do not drag the preparation `.blend` into Assets.

## Step 4B — Character FBX settings

Select `Jiwoo_Normalized.fbx` in the Project window, then set:

| Tab | Setting | Value |
| --- | --- | --- |
| Model | Scale Factor | 1 |
| Model | Convert Units | Enabled |
| Model | Bake Axis Conversion | Enabled |
| Model | Import BlendShapes | Enabled |
| Model | Mesh Compression | Off |
| Model | Read/Write | Enabled for the validation/setup checkpoint |
| Model | Import Cameras / Import Lights | Disabled |
| Model | Skin Weights | Custom |
| Model | Max Bones/Vertex | 8 |
| Model | Min Bone Weight | 0.001 |
| Rig | Animation Type | Humanoid |
| Rig | Avatar Definition | Create From This Model |
| Rig | Optimize Game Objects | Disabled |
| Animation | Import Animation | Disabled |
| Materials | Material Creation Mode | None |

Click **Apply**. The Humanoid avatar should be valid. You can inspect Configure,
but do not change the bone pose as part of this import checkpoint.

The FBX declares meters in its unit metadata. Its geometry is not multiplied by
100 in object transforms. Do not add a 0.01 scale on the scene instance.

## Step 4C — Alembic settings

Select `Jiwoo_Bob_HeadLocal.abc` and set:

| Setting | Value |
| --- | --- |
| Scale Factor | 1 |
| Swap Handedness | Enabled |
| Import Curves | Enabled |
| Add Curve Renderers | Disabled |
| Import Meshes | Disabled |
| Import Cameras | Disabled |
| Import Points | Disabled |

Click **Apply**. The file contains one static sample; Unity reports its time
range as zero. Leave its imported time range alone.

The Alembic importer defaults to 0.01; deliberately replace that with **1** for
this new export. These settings apply to the new files, not the previous v3 ABC.
The required FBX/Alembic axis correction is already baked into the new groom.

Keep the ABC as a Project asset. Its coordinates are relative to the head, so
dragging it directly into the scene does not create the correctly attached
simulated hairstyle. Step 5 builds a Hair Asset from it and assigns that asset
to a Hair Instance beneath the new character's `head` bone.

## Step 4D — Place and check the new character

1. Drag `Jiwoo_Normalized.fbx` into the Hierarchy as a new instance.
2. Set its root Transform to position `(0,0,0)`, rotation `(0,0,0)`, scale
   `(1,1,1)` for the comparison checkpoint. Keep other characters out of the way.
3. Select its `Face` object and assign the existing materials from
   `Assets/_Sandbox/Characters/Jiwoo/Materials` to the Skinned Mesh Renderer:

| Material element | Existing material |
| --- | --- |
| 0 — head | Jiwoo_Head |
| 1 — teeth | Jiwoo_Teeth |
| 2 — saliva | Jiwoo_Saliva |
| 3 — left eye | Jiwoo_Eyes |
| 4 — right eye | Jiwoo_Eyes |
| 5 — eye shell | Jiwoo_EyeShell |
| 6 — eyelashes | Jiwoo_Eyelashes |
| 7 — eye edge | Jiwoo_EyeEdge |
| 8 — cartilage | Jiwoo_Cartilage |
| 9 — body | Jiwoo_Body |

4. Confirm the character is approximately 1.75 m tall. The source is in an A-pose.
5. Find `Jiwoo_Armature/root/pelvis/spine_01/spine_02/spine_03/spine_04/spine_05/neck_01/neck_02/head`.
   The imported head and ancestor scales should be approximately one, not 100.
   The isolated check measured head world scale `(1.0000001, 1.0000004, 1.0000006)`.
6. On `Face`, confirm the BlendShapes list contains 73 shapes. Temporarily move
   `eyeBlinkLeft` and `jawOpen` to 100 one at a time; return both to zero afterward.
7. Save the instance as a **Prefab Variant**, named `Jiwoo_Normalized.prefab`,
   in the new `JiwooNormalized` folder so it keeps its FBX link and material overrides.

Stop at this checkpoint. The character will still be bald until Step 5 creates
and assigns the Hair Asset. Play Mode is not needed for Step 4.

## Contract for the following hair setup

This records the intended settings for Step 5; it is not a claim of simulation
validation. Create a new Hair Asset rather than reusing Female Bob Hair from the
previous workflow.

- Alembic position and diameter source units: **Meters**.
- Width from curves: imported widths are 0.0001 m, meaning **0.1 mm diameter**.
- Hair Asset root UV: fallback/constant initially; scalp UV transfer can follow.
- New Hair object directly under the new `head`, position/rotation zero, scale one.
- Hair Instance Strand Scale: **Fixed**. No length/diameter override initially.
- Assign `Jiwoo_StrandHair` explicitly and compare the unsimulated resting shape
  before tuning forces, collisions, LODs, or warmup.
- ABC has 64 arc-length samples per strand. Keep that detail for the first static
  comparison; choose simulation resolution and LODs in Step 5 after observing cost.

## Verification evidence and limits

- Blender FBX readback: 64,300 vertices, all 1,185 bones and their parents, skin
  weights, 10 material slots, and 73 shape keys retained. Neutral mesh maximum
  displacement was 0.000307 mm; maximum shape-key displacement was 0.001462 mm.
- Native Bezier strands were sampled to 64 points by arc length. Against dense
  Bezier samples, 99th-percentile maximum-per-strand deviation is 0.236 mm, and
  the worst strand is 1.653 mm. This is a sampled approximation bound, not an
  analytic bound on the continuous spline. Native source curves remain preserved.
- Blender ABC readback: all 100,493 x 64 points survived exactly; diameter 0.1 mm.
- Unity: valid Humanoid avatar; root and head world scales approximately one;
  all 73 BlendShape names preserved. Unity splits vertices at rendering seams,
  resulting in 66,963 vertices, which is expected.
- Unity world geometry surface comparison: maximum deviation approximately
  0.001098 mm. Head/ABC alignment across 2,048 distributed samples: maximum
  deviation approximately 0.000470 mm. All 1,185 joint positions also agree with
  the source coordinate conversion within 0.000827 mm.
- Unity imported ABC widths are 0.0001 m. Alembic and FBX verified settings above
  are paired; changing handedness or bake-axis settings changes alignment.
- This validates export/import and static attachment coordinates. Hair Asset
  generation, runtime simulation, collisions, rendering, and performance still
  need validation after the user's Unity setup.

Detailed evidence is in `Data/JiwooReimport/Batch03/`: `export_report.json`,
`blender_readback_report.json`, `unity_import_report.json`, and
`unity_verification_report.json`. `handoff_manifest.json` identifies the final
production files by SHA-256.
