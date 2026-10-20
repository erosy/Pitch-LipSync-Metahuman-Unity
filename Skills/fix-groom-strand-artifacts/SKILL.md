---
name: fix-groom-strand-artifacts
description: Diagnose and repair strand grooms when hairs become long straight lines, acquire invalid endpoints, lose their intended shape, or penetrate a character's skin during Blender, Alembic, or Unity Hair workflows. Use for curve-data defects, groom export/import problems, attachment errors, and strand collision troubleshooting; not for ordinary hairstyle sculpting or hair-card authoring.
---

# Fix Groom Strand Artifacts

Restore the intended groom without hiding a coordinate, topology, attachment, or
simulation defect. Preserve the source file and work on a versioned copy or new
export. Do not overwrite a known-good ABC until the replacement passes readback.

For this project's verified units, axes, export options, and Unity configuration,
read [CharacterGroomWorkflow.md](../../Docs/CharacterGroomWorkflow.md). For the
Jiwoo-specific handoff, read
[JiwooReimportUnityImport.md](../../Docs/JiwooReimportUnityImport.md). Treat its
object names and counts as Jiwoo-specific, not universal requirements.

## Classify the failure before editing

Inspect the native groom, exported ABC, Hair Asset preview, and unsimulated Hair
Instance separately. Identify the earliest stage where the defect appears.

| First defective stage | Likely cause |
| --- | --- |
| Native Blender groom | Bad point/handle, corrupt strand, unintended cyclic curve, or modelling defect |
| ABC readback only | Unevaluated spline export, topology padding, invalid width/data, or coordinate conversion |
| Hair Asset preview only | Wrong ABC reference, stale strand groups, wrong units, or inappropriate resampling |
| Hair Instance with simulation off | Wrong attachment/transform, geometry override, or stale generated instance |
| Hair Instance only after simulation | Missing/incorrect constraints, solver instability, or collision setup |

Do not use collision settings to disguise a defect already present with simulation
off. Do not edit generated `Group`, `Roots`, or `Strands` children; they can be
recreated when the Hair Asset changes.

## Audit curve data

Record curve count, total point count, points per curve, bounds, transforms,
root-to-tip order, cyclic state, widths, and non-finite values. Compare these at
the native, export-derivative, ABC-readback, and Unity Hair Asset stages.

Detect suspicious straight segments using geometry, not appearance alone:

- Measure every consecutive segment length and compare it with neighbouring
  segments and the strand's median segment length.
- Flag endpoints whose segment is a strong outlier, whose position is shared by
  many otherwise unrelated curves, or which jumps toward the object/world origin.
- Check both ends before deciding which is the root. Confirm root proximity to the
  scalp and the intended root-to-tip order.
- Check whether an importer forced equal point counts by appending or repeating a
  point. A point-count difference alone does not prove corruption.
- Inspect spline evaluation and Bezier handles. A sparse control polygon can look
  like straight spikes even when no point was added.

Only remove an endpoint when the data establishes that it is an appended outlier.
Never delete the last point of every strand as a generic fix. Preserve valid flyaway
hairs; they may be long but should remain locally continuous with their strand.

## Repair the earliest defective representation

For isolated invalid endpoints, remove only the confirmed points or curves. For
bad handles or insufficient evaluation, evaluate the native spline correctly and
resample by arc length. For mirrored, rotated, or displaced grooms, repair the
coordinate-space conversion rather than compensating with arbitrary prefab
rotations.

Prepare the export groom in head-local coordinates:

```text
head_local_point = inverse(head_world_matrix) * groom_world_matrix * point
```

Apply the same conversion to curve handles. Export an unparented derivative with
identity transform while preserving the editable native groom. Maintain explicit
meter units, widths, root-to-tip order, and curves rather than meshes. Use the
project documentation for the verified Blender/Unity handedness conversion.

Read the exported ABC back before Unity. Reject it if counts unexpectedly change,
outlier segments remain, roots move away from the scalp, widths become implausible,
or the silhouette differs materially from the source.

## Diagnose skin penetration

First disable simulation and reset the Hair Instance.

- If strands intersect the face in the resting groom, repair or refit the groom in
  Blender. Collision cannot reliably fix a bad rest shape.
- If the resting groom is correct but roots detach when the head moves, verify that
  the Hair object is a direct child of the correct imported `head` bone with local
  position/rotation zero and scale one. Verify matching FBX and ABC coordinate
  profiles.
- If roots stay attached but moving strands enter the skull, add a fitted Hair
  Boundary or character-specific head SDF and explicitly register it with the Hair
  Instance.
- If only a few face-framing strands intersect in neutral, reshape or trim those
  strands while preserving their roots and the hairstyle silhouette.

Do not reuse an SDF fitted to another character. Start collision boundaries inside
the scalp and validate front, side, and back views. Excessive collision margins can
inflate the hairstyle; overly small or misplaced boundaries allow penetration.

## Unity isolation sequence

Keep the existing working character for comparison and create new versioned
assets. For this project's verified groom profile:

1. Import the ABC with scale factor `1`, Swap Handedness enabled, curves enabled,
   and automatic curve renderers disabled.
2. Create a new Hair Asset. Assign the intended ABC explicitly; an Alembic-type
   Hair Asset with an empty Alembic field may continue rendering stale strand data.
3. Begin without resampling or LOD reduction when the ABC already has the verified
   sampling. Use meter source units and build strand groups.
4. Confirm the Hair Asset preview retains the complete resting silhouette.
5. Create `Hair` under the correct head bone with identity local transform, add a
   Hair Instance, disable simulation, and assign the new Hair Asset.
6. Use Fixed strand scale and disable length/diameter overrides for the first
   comparison. Assign the intended strand material explicitly.
7. If the instance remains stale, remove and re-add the provider so Unity rebuilds
   its generated group objects.
8. Enable simulation constraints only after the static fit passes. Add collision
   only after constraint behavior passes.

If the Hair Asset preview is correct but the instance is not, investigate the
instance. If the preview is already wrong, return to the Hair Asset or ABC rather
than tuning physics.

## Validation

Require all of the following before replacing the previous groom:

- Native and ABC readback have expected curve/point counts and finite data.
- No unexplained endpoint or segment-length outliers remain.
- Root-to-tip order, widths, units, bounds, and coordinate frame are documented.
- The unsimulated Unity groom matches front, side, and back references at scale one.
- Roots follow the head during a controlled Edit Mode attachment check.
- Simulation constraints preserve the recognizable silhouette.
- Character-specific boundaries prevent agreed head, ear, face, and neck
  penetrations without visibly inflating the groom.
- Reopening the prefab preserves the Hair Asset, material, and boundary references.

The user controls Unity Play Mode. Do not enter or exit it automatically. Use
compilation and Edit Mode checks unless the user explicitly requests otherwise,
and do not run WebGL builds unless requested.
