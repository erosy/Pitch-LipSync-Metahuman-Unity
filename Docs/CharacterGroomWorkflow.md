# Character and groom workflow: Blender to Unity

Audience: 3D modeller, technical artist, and Unity integrator.

Purpose: deliver a character and strand hairstyle that import at the correct size,
fit the same head, preserve facial animation, and use **Fixed** strand scale in
Unity's Demo Team Hair package. Work in batches and pass each checkpoint before
continuing.

This is a standalone instruction document, not an installed Codex skill. It can
also be given to an assistant as project instructions. The modeller prepares and
exports; the Unity integrator imports and configures the assets. In the current
project, the user performs the Unity steps unless explicitly requesting automation.

## 1. Delivery contract

Deliver the following together for every character revision:

```text
CharacterName_Delivery/
  Source/
    CharacterName_Authoring.blend
    CharacterName_ExportPrepared.blend
  Export/
    CharacterName_Normalized.fbx
    CharacterName_Hairstyle_HeadLocal.abc
    Textures/
  Reference/
    Front.png
    Side.png
    Back.png
  README.md
```

The delivery README must state:

- Blender version, export settings, intended Unity version, character height in
  meters, and the exact revision shared by the FBX and ABC.
- The export rig, head attachment bone, rest pose, and facial-shape names.
- Material-slot order, texture assignments, alpha usage, and any texture-channel
  packing or tangent-normal convention.
- Groom curve count, points per exported curve, maximum strand length in meters,
  strand diameter in millimeters, coordinate conversion, and root-to-tip order.
- Which facial features are included: eyebrows, eyelashes, beard/moustache where
  applicable. State whether they are painted textures, hair cards, meshes, or
  strand grooms. Explicitly identify anything missing.
- Which checks passed and which work remains, such as simulation, collisions,
  material matching, animation, and performance tuning.

The `.blend` source files stay outside Unity's `Assets` folder. Import the FBX,
ABC, and required textures into Unity. Do not import audit files, numeric
baselines, or source scripts as game assets.

### Coordinate and unit standard

| Item | Required result |
| --- | --- |
| Blender scene | Metric, Unit Scale 1, Length Meters |
| Character size | Physically plausible dimensions, documented in meters |
| Export rig and mesh object scale | Approximately `(1,1,1)` |
| Imported Unity root and head world scale | Approximately `(1,1,1)` |
| Groom positions and ABC widths | Meters |
| Blender radius | Half the intended strand diameter |
| Unity Hair object | Head child, position zero, rotation zero, scale one |
| Unity Hair Instance Strand Scale | Fixed |

For this profile, **1 Blender unit means 1 meter**. A 1.75 m character should
import near 1.75 Unity units tall without a scene-instance scale correction.
Correct object scales in Blender alone do not prove that the exported bones will
import correctly; check the exported files as well.

## 2. Modeller batches: prepare and export

### Batch 1 — Audit the source

1. Preserve the authoring file and work from a separate export copy.
2. Measure character height and inspect the rig, mesh transforms, head bone,
   groom, modifiers, constraints, drivers, material slots, and external images.
3. Identify the intended export skeleton and meshes. Authoring rigs, GUI widgets,
   duplicate body/head meshes, and control shapes must not accidentally export.
4. Check the neutral body pose and neutral facial expressions. Record all shape
   keys and skin weights before making changes.
5. Inspect the face with scalp hair hidden. Confirm that eyebrows and eyelashes
   actually exist and are visible.

**Checkpoint:** the source has the expected appearance, known dimensions, a
specified export rig, and a complete list of deliverables or missing features.

### Batch 2 — Prepare a clean export copy

- Preserve the export skeleton's hierarchy, bind pose, skin weights, UVs, material
  assignments, and facial shapes. Do not blindly apply transforms to an already
  skinned rig or rebuild its rest pose.
- Resolve authoring dependencies before excluding authoring objects. A model that
  relies on a missing DNA solver, addon, or driver needs evaluated export data.
  Do not enable embedded scripts automatically merely to open a supplied file.
- Keep the character neutral. Remove unwanted animation from the export copy;
  preserve animation deliverables separately if requested.
- Attach the groom to the **export rig's head**, rather than a duplicate authoring
  head rig. Preserve its world-space fit during the change.
- Store the groom's positions and Bezier handles relative to that export head in
  meters. Preserve the editable native groom separately from export derivatives.
- Confirm each strand starts at the scalp and ends at the tip. Root-to-tip order
  matters for attachment and tapering.
- Set an explicit strand radius. The Jiwoo starting diameter is **0.1 mm**, so its
  Blender radius is **0.00005 m**. This is an initial artistic width, not a universal
  requirement for every character or a performance budget.
- Pack required images or supply them in a complete texture folder. Remove hidden
  authoring objects only from the export copy after checking the evaluated result.

**Checkpoint:** reopening the prepared copy preserves the character, facial
shapes, skinning, and groom fit. A small temporary head rotation carries the groom
correctly. Restore the neutral pose before saving/exporting.

### Batch 3A — Export the character FBX

Select only the export rig and intended skinned meshes. Jiwoo uses
`Jiwoo_Armature` and the combined mesh `Face`; separate skinned meshes are also
acceptable for other models if their materials and bindings are documented.

Use the following verified Blender FBX profile:

| Export setting | Value |
| --- | --- |
| Selected Objects | On |
| Object Types | Armature and Mesh |
| Scale | 1 |
| Apply Unit | On |
| Apply Scalings | FBX Units Scale (`FBX_SCALE_UNITS`) |
| Forward / Up | `-Z Forward`, `Y Up` |
| Use Space Transform | On; exporter API option |
| Apply Transform | Off (`bake_space_transform=False`) |
| Apply Modifiers | Off to preserve shape keys in this workflow |
| Primary / Secondary Bone Axis | Y / X |
| Add Leaf Bones | Off |
| Only Deform Bones | Off for this profile; retain facial/attachment bones |
| Bake Animation | Off for the neutral character delivery |
| Smoothing | Face |
| Embed Textures | Off; deliver image files separately |

The export script in this project records the exact API flags, including options
that may not be exposed under the same label in every Blender version.

Deliver the character's facial expression and lip-sync shapes by name. Preserve
the agreed project schema; Jiwoo has 73 non-Basis shapes. Do not require every
future character to have Jiwoo's vertex, bone, or shape count unless the project
specifically requires compatibility with that schema.

### Batch 3B — Export the groom ABC

The character uses FBX; the simulated scalp hair uses a **separate Alembic curves
file**, not a converted hair mesh and not a baked animation cache.

1. Make an export derivative of the fitted groom.
2. Evaluate Bezier curves, including handles, before sampling. Exporting only the
   control polygon can change the hairstyle.
3. Sample by arc length so particles are reasonably evenly spaced. Jiwoo's export
   uses 64 points per strand; retain the native curves for later resampling.
4. Bake the agreed head-local coordinate and axis conversion into the export
   points. Export an unparented derivative with identity object transform so the
   archived curve data does not depend on an authoring parent transform.
5. Store explicit widths. Check the exported values: Blender radius 0.00005 m
   should become Alembic **diameter 0.0001 m**, not radius 0.0001 m.

| Alembic export setting | Value |
| --- | --- |
| Selection Only | On; export the derivative groom only |
| Frame range | One neutral frame; Jiwoo uses frame 1 to 1 |
| Global Scale | 1 |
| Flatten Hierarchy | On |
| Curves as Mesh | Off |
| Export Hair | On |
| Export Particles | Off |
| Export Custom Properties | Off for this profile |

**Head-local coordinates need an explicit conversion.** Zeroing the groom's
object position is not the same as converting its geometry into head space.
For source point `p`, groom world matrix `G`, and head world matrix `H`:

```text
head_local_point = inverse(H) * G * p
```

Transform Bezier handles consistently. Handle Blender bone-parent offsets as
part of the preparation; the visible fit must remain unchanged.

For the **verified Jiwoo exporter profile**, FBX bone import and ABC handedness
conversion differ. The exporter compensates as follows, where `(hx,hy,hz)` is a
point in the prepared Blender head frame:

```text
Blender derivative position before ABC export = (-hx, hz, hy)
ABC position after Blender's Z-up to Y-up conversion = (-hx, hy, -hz)
Unity ABC import with Swap Handedness on = (hx, hy, -hz)
```

That final coordinate matches the verified FBX's imported head frame. This is
**baked point data**, not an instruction to rotate the visible groom in the
authoring scene. Do not apply the correction again to an already prepared ABC.

This correction belongs to the stated exporter/importer profile. A different rig,
bone-axis convention, Blender version, or FBX axis option must be checked by
readback; do not assume the same correction without verification. Plain Alembic
export settings alone do not perform this head-relative preparation.

### Batch 3C — Read the exports back before delivery

- Reimport the FBX in a separate scene/process and compare neutral geometry,
  hierarchy, skin weights, material slots, and **every** facial shape.
- Reimport the ABC and check strand count, point count, root-to-tip order, width,
  bounds, and shape against the intended sampled curves.
- Compare the sampled groom against dense native-curve evaluations. Report
  sampling error separately from export/import error; sampled curves are an
  approximation of the original spline.
- Run an isolated Unity import using the target Editor/package versions. Confirm
  root/head world scales, valid avatar, facial shapes, and groom alignment in the
  imported head frame. Record whether a measurement covers all data or a sample.
- Include front, side, and back references, settings, and final file hashes.

**Checkpoint:** reject unexplained 100x scale, mirrored or rotated hair, missing
expressions, missing textures/features, or shifted roots. Fix the export/profile
before asking the integrator to compensate in a prefab.

## 3. Unity integrator batches: import, assign, and attach

### Batch 4 — Import a new character variant

Import the FBX and ABC into a new source folder. Preserve the working character
for comparison. Do not overwrite existing sources or copy unrelated `.meta` files
into a new character delivery.

FBX settings:

| Tab | Setting | Value |
| --- | --- | --- |
| Model | Scale Factor / Convert Units | 1 / On |
| Model | Bake Axis Conversion | On |
| Model | Import BlendShapes | On |
| Model | Mesh Compression | Off |
| Model | Read/Write | On if needed for setup/measurement; review afterward |
| Model | Import Cameras / Lights | Off |
| Model | Skin Weights | Custom, maximum 8, minimum weight 0.001 for this profile |
| Rig | Animation Type | Humanoid |
| Rig | Avatar Definition | Create From This Model |
| Rig | Optimize Game Objects | Off during attachment/setup |
| Animation | Import Animation | Off for this neutral FBX |
| Materials | Material Creation Mode | None; assign project materials explicitly |

Apply the settings. Confirm the Humanoid avatar is valid without changing the
rest pose to compensate for a groom alignment issue. Test each required facial
shape, then restore its weight to zero.

ABC settings:

| Setting | Value |
| --- | --- |
| Scale Factor | **1**; override Alembic's default 0.01 |
| Swap Handedness | On |
| Import Curves | On |
| Add Curve Renderers | Off |
| Import Meshes / Cameras / Points | Off |

Apply and keep the imported static time range. Do not assume a single exported
frame creates a nonzero Unity time range; Jiwoo imports at time zero.

Create a scene instance with root scale one and verify its documented physical
height. Save a Prefab Variant to retain the FBX link and subsequent overrides.

### Materials and facial hair

FBX material slots are not a transfer of Blender's full shading system. Assign
Unity-compatible materials and check the appearance. With Material Creation Mode
None, default Lit materials can appear until those assignments are made.

Jiwoo's combined `Face` renderer has the following **zero-based** slot order:

| Slot | Surface | Unity material |
| --- | --- | --- |
| 0 | Head | Jiwoo_Head |
| 1 | Teeth | Jiwoo_Teeth |
| 2 | Saliva | Jiwoo_Saliva |
| 3 | Left eye | Jiwoo_Eyes |
| 4 | Right eye | Jiwoo_Eyes |
| 5 | Eye shell | Jiwoo_EyeShell |
| 6 | Eyelashes | Jiwoo_Eyelashes |
| 7 | Eye edge | Jiwoo_EyeEdge |
| 8 | Cartilage | Jiwoo_Cartilage |
| 9 | Body | Jiwoo_Body |

Materials are in `Assets/_Sandbox/Characters/Jiwoo/Materials`. Other characters
must deliver their own slot map; do not assume Jiwoo's order is universal.
Save the assignments on the prefab, then verify the scene instance inherits them.

For newly imported textures, use sRGB for base colors, Normal Map import type for
normal textures, and linear sampling for data masks. Set alpha clipping or
transparency deliberately for lashes/cards. Check the supplied normal-map and
channel-packing conventions rather than guessing.

Eyebrow expression controls do **not** create visible eyebrow hair. The inspected
Jiwoo source has no eyebrow groom/visible brow mesh and no painted eyebrows in
its head base color. Material assignment cannot restore a feature absent from
the source. The modeller must supply painted brows or geometry/grooms as a
separate follow-up. Brow geometry must follow facial expressions, not just rigid
head motion; scalp-hair simulation settings are not automatically appropriate
for eyebrows and eyelashes.

### Batch 5 — Build the Hair Asset and check the resting shape

Create a **new** asset via `Create > Hair > Hair Asset`:

| Setting | Initial comparison value |
| --- | --- |
| Type | Alembic |
| Alembic Asset | New head-local ABC |
| Alembic Asset Groups | Combine |
| Position source units | Data In Meters |
| Diameter source units | Data In Meters |
| Resample Curves | Off; preserve the exported 64 points initially |
| Root UV | Use Fallback for the initial uniform-color comparison |
| Strand Diameter | Resolve From Curves |
| Diameter fallback | 0.1 mm for Jiwoo |
| Tip Scale | Use Fallback, value 0.15, offset 0.75 |
| K LOD Clusters | Off for the initial full-detail comparison |
| Auto rebuild | Off |

Click **Build strand groups**. Jiwoo should have one group, 100,493 strands,
and 6,431,552 particles. Constant root UV is a temporary starting point; a
UV-dependent material needs a separate verified UV transfer.

Create a new `Hair` GameObject directly under the imported `head` bone:

```text
Position: 0, 0, 0
Rotation: 0, 0, 0
Scale:    1, 1, 1
```

Add **Hair Instance**. Disable **Update Simulation** before assigning the Hair
Asset under **System Contents**. Under **Defaults (All Groups)**:

- Geometry: **Strand Scale Fixed**, length and diameter overrides off, separation
  zero, staging subdivision off.
- Rendering: enable Material, assign the strand-hair material, Renderer
  **Builtin Strips**. Jiwoo uses `Jiwoo_StrandHair`.

Do not manually change the generated Group/Roots/Strands objects. Compare front,
side, and back with the source references. Roots must fit the scalp and the bob
must retain its complete silhouette. Save the prefab.

**Checkpoint:** correctly fitted, correctly sized resting hair with simulation
off. Fix coordinate or export errors here before tuning physics.

## 4. Simulation, collision, and final acceptance

### Batch 6 — Establish a simulation baseline

The following values are an **initial diagnostic profile**, not a validated
production preset for every hairstyle:

| Settings Physics | Starting value |
| --- | --- |
| Solver / Substeps / Iterations | Gauss Seidel / 2 / 8 |
| Constraint Stiffness / SOR | 1 / 1 |
| Gravity | 0.2 |
| Cell Pressure / Cell External | 0 / 0 to isolate shape behavior |
| Boundary Collision | Off until a boundary is fitted |
| Distance / Distance LRA | On / On |
| Distance FTL / Local Curvature | Off / Off |
| Local Shape | On, Stitched, influence 1 |
| Local Shape Bias | On, 0.7 |
| Global Position | On, 0.3 per 100ms |
| Global Rotation | On, 0.7 |
| Global Fade | Off |
| Linear Damping | On, 0.2 per 100ms |

Use Builtin Event scheduling, initially Fixed 30 Hz, minimum steps off and
maximum steps 2. Enable Update Simulation and In Editor, then reset the solver.
Enable Scene view Always Refresh when testing continuous Edit Mode simulation.

Observe at rest, then gently translate/rotate the whole character and restore its
original transform. Keep scale one. Hair ends may swing; roots should remain on
the scalp. Test 60 Hz if necessary, while watching performance. Forcing a minimum
step every rendered frame can advance simulated time faster than real time;
do not use it as an unexplained fix.

Diagnose apparent temporary baldness before changing timing or attachment:

| Observation | Next investigation |
| --- | --- |
| Roots stay attached, strands swing through skull | Fit head collisions |
| Whole root field separates from the moving head | Check attachment/update timing |
| Wrong fit with simulation off and reset | Check export coordinates and transforms |

In Jiwoo's test, the reported apparent lag was clarified as strands passing
through the head before collisions were added. A timing defect was not established.

### Batch 7 — Add a head collision boundary

1. Create `Hair_HeadBoundary` under the head, with scale one. Add **Hair Boundary**,
   Mode **Standalone**, Type **Sphere**.
2. For Jiwoo, radius around **0.08 m** is only a starting estimate. Fit it inside
   the upper skull using front/side views and visible Gizmos. Use the Global move
   gizmo if the bone's local axes are confusing. Keep the boundary inside the
   scalp, not around the nose or outside the roots.
3. In Hair Instance > Settings Environment, disable automatic Boundary Capture
   for this controlled test, and add this Hair Boundary to **Boundary Resident**.
4. Start with Default Solid Margin **0.1 cm = 1 mm**. This field uses centimeters.
5. Enable Boundary Collision in Settings Physics; start friction at **0.3**.
6. Reset and repeat gentle motion. If the bob inflates abruptly, inspect boundary
   fit, margin, and pre-existing strand intersections before increasing stiffness.

A regular Unity collider alone is not the same as an explicitly assigned Hair
Boundary in this configuration. A Rigidbody is not needed for the standalone
sphere. A sphere is an initial approximation; refine the skull/ear/neck fit or
bake a character-specific SDF if needed. Do not reuse Ethan's fitted SDF for a
different head without rebaking and validating it.

### Batch 8 — Validate and optimize

- Check resting and moving hair from front, side, and back, including head turns,
  blinks, eyebrow expressions where supported, jaw opening, and lip-sync shapes.
- Confirm roots track, the silhouette stays recognizable, and head/ear/neck
  penetration is acceptable for the agreed poses.
- Compare the saved/reopened prefab with the working scene. Confirm material
  assignments and Hair Asset references persist.
- Profile before increasing substeps/iterations or using the setup on several
  characters. Jiwoo's 100,493 x 64 starting asset is over **6.4 million particles**
  before LOD reduction; it is a visual baseline, not a shipping budget.
- Evaluate fewer simulation points, LOD clusters, guide density, rendering LODs,
  and quality settings separately. Preserve the native groom and full-detail
  reference so reductions can be compared.
- Record final settings and unresolved issues. Export/import verification alone
  does not establish runtime stability or performance.

The user controls Unity Play Mode. Do not automatically enter/exit it. Routine
project verification uses compilation and relevant Edit Mode checks; do not run
WebGL builds unless explicitly requested. A web target needs a separate hair
rendering/simulation compatibility plan and potentially a card/mesh fallback.

## 5. Handoff checklist and project references

### Modeller sign-off

- [ ] Authoring source and export copy are both supplied.
- [ ] Units, dimensions, export rig, and head coordinate frame are documented.
- [ ] FBX contains the intended meshes, skeleton, weights, UVs, and facial shapes.
- [ ] ABC contains root-to-tip curves, evaluated shape, explicit widths, and the
  verified axis conversion.
- [ ] Required textures and material-slot map are supplied.
- [ ] Eyebrows, eyelashes, and other expected facial hair are supplied or clearly
  marked missing; rig controls are not counted as visible hair.
- [ ] Export readback checks and front/side/back references are included.

### Unity sign-off

- [ ] Root and head world scales are approximately one; avatar and expressions work.
- [ ] Materials match the documented slots and survive prefab reload.
- [ ] New Hair Asset uses meter units and a head-child Hair Instance using Fixed.
- [ ] Resting shape matches before simulation is enabled.
- [ ] Simulation and collisions pass separately, including head movement.
- [ ] Performance/LOD and target-platform suitability are checked before shipping.

### Jiwoo implementation references

- [Verified Unity import checklist](JiwooReimportUnityImport.md).
- [Source audit](JiwooReimportBatch01.md) and
  [prepared Blender scene checks](JiwooReimportBatch02.md).
- Export and verification scripts: `Tools/Hair/audit_jiwoo_blender.py`,
  `prepare_jiwoo_export.py`, `export_jiwoo_batch03.py`,
  `verify_jiwoo_batch03_blender.py`, `JiwooExportVerification.cs`, and
  `check_jiwoo_unity_import.py`.
- Prepared source: `Data/JiwooReimport/Batch02/Jiwoo_ExportPrepared.blend`.
- Verified delivery: `Data/JiwooReimport/Batch03/UnityImport`.
- Detailed reports and checksums: `Data/JiwooReimport/Batch03`.

The current scripts contain Jiwoo-specific object names, output paths, counts,
and axis assumptions. Treat them as the verified reference implementation;
parameterize and revalidate them before using them for another model. Some
export commands overwrite their generated outputs, so use a separate revision
directory for each new delivery.

Verified export/import baseline: Blender **5.1.2**, Unity **6000.5.9f1**, Unity
Alembic **2.4.7**, and this project's embedded Demo Team Hair package. Recheck
the contract when changing these versions or the rig/export conventions.
