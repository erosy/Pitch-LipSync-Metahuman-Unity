# Jiwoo reimport — Batch 1 source inspection

## Source and method

- Source: `C:/Users/ina17/Downloads/Jiwoon Eros Try - Fixed v3.blend`.
- Inspected with Blender 5.1.2 in background mode, factory startup, and embedded
  script execution disabled. The source was not saved or modified.
- Reproduce with `Tools/Hair/audit_jiwoo_blender.py`; machine-readable results
  are in `Data/JiwooReimport/Batch01/source_audit.json`.
- Unity assets and settings were not changed in this batch.

## Findings

- Scene units are Metric, unit scale 1, length unit Meters.
- The combined export mesh `Face` is approximately 1.7513 m tall and has 64,300
  vertices. Its object scale is 1 and its Armature modifier targets
  `Jiwoo_Armature`.
- `Face` retains Basis plus 73 named shape keys, including facial expressions,
  lip-sync visemes, and emotion shapes. No animation Actions are present.
- `Jiwoo_Armature` has 1,185 bones. Its object scale and the sampled root,
  pelvis, neck, and head world scales are approximately 1.
- `Hair` is a native Curves object with 100,493 curves and 1,353,886 control
  points. It has Bezier handle attributes and no modifiers. The export must
  preserve evaluated Bezier shapes, not treat control points as straight segments.
- Hair world bounds are approximately 0.2796 x 0.2764 x 0.3074 m in Blender
  axes, with height from 1.4821 to 1.7895 m. The bob and character already have
  plausible proportions in the source.
- Hair is bone-parented to `head` on
  `Jiwoo_MetaHumanCharacter_head_rig`, rather than `Jiwoo_Armature`. Its parent
  transform and local offset must be baked while retaining the world-space fit.
- Hair has no explicit radius/width attribute. A documented width must be
  assigned and its exported Alembic representation verified before Unity setup.
- Multiple authoring rigs and duplicate source meshes coexist with the combined
  export character. Export selection must include only the intended character
  rig/mesh and separately prepared groom.
- Native Character DNA drivers cannot execute in the isolated inspection.
  The export pair has no object drivers, but evaluated/rest-pose equivalence
  still needs checking before the authoring objects are removed from an export copy.
- Two missing images are authoring topology references (`body_topology.png` and
  `head_topology.png`). No other unpacked file-image references were missing.

## Batch 2 preparation requirements

1. Create a separate preparation `.blend` under `Data/JiwooReimport`, preserving
   the original Downloads file.
2. Use `Face` and `Jiwoo_Armature` as the character export pair. Preserve all
   shape keys, weights, material slots, and bone hierarchy.
3. Compare neutral evaluated geometry and head frames before isolating the
   export objects. Resolve dependencies without running embedded source scripts.
4. Bake the groom's existing parent transform into a documented head-relative
   coordinate frame in meters. Preserve all curves and Bezier shape.
5. Set an explicit, documented strand diameter and verify how the Alembic
   exporter writes it. Diameter/radius conversion must be explicit.
6. Run FBX and ABC round-trip checks outside Unity Assets. Select final exporter
   options based on measured round-trip scale and orientation, rather than
   assuming object transforms guarantee Unity bone scale 1.
7. Produce the exact Unity import instructions only after these checks pass.

## User handoff

This batch needs no Unity action. Batch 2 prepares and checks the export copy;
Batch 3 produces and verifies export files. The user performs Unity importing,
Hair Asset/Instance setup, and validation using subsequent batch instructions.
The existing Unity character remains available for comparison.
