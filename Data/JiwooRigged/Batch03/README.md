# Batch 3 cleanup completed

- Removed 41 obsolete or unused Unity assets after a project-wide dependency
  audit found no external references. This includes the old Jiwoo models,
  grooms, texture copies, and unused facial material; matching metadata was
  removed through Unity's AssetDatabase.
- Removed the redundant hairstyle selector from the prefab and scene, then
  removed its unreferenced script and metadata.
- Removed 154 obsolete files in the old reimport workspace, repair/export
  scripts, historical Jiwoo instructions, pre-install backups, and temporary
  Unity geometry dumps. These non-asset files occupied 1,368,839,321 bytes.
  The one-time cleanup scripts were also removed after use.
- Retained the corrected Blender bake, current FBX/ABC/texture sources, required
  numerical baselines, concise verification reports, and five current pipeline
  scripts with a standalone 73-name facial interface. The bake no longer reads
  any old import reports. The importer no longer needs legacy groom templates or
  the removed hairstyle selector.
- Updated the general groom workflow to link the current
  [Jiwoo handoff](../../../Docs/JiwooRigged.md).

The shared warmup, fallback, and readiness improvements and their relevant tests
remain. Their three script GUIDs match the former Ethan script GUIDs. Active
Jiwoo prefab/material GUIDs are preserved.

Final static checks passed for Jiwoo's saved prefab and scene, valid Humanoid,
73 shapes, materials, grooms, lip-sync indices, and Girl/Boy response bindings.
The scene and Jiwoo/Ethan prefabs contain no missing asset GUIDs or scripts.
Ethan retains all three shared support components. Unity compilation reported
up to date, with zero compiler errors or warnings.

No Play Mode changes, live testing, audio/animation playback, or builds were
performed. Facial groom deformation, artistic expression quality, outfit motion,
and runtime rendering/performance remain user review as documented in the
current handoff.

Deletion manifests: `deleted_assets.json` and `deleted_files.json`.
Final static checks: `saved_verification.json`.
