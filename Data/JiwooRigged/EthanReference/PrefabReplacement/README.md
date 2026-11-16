# Jiwoo_Normalized model replacement

The existing `Assets/_Sandbox/Characters/Jiwoo/Jiwoo_Normalized.prefab` now uses
`Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx`. Its original GUID
`48dcd3a0926950846ad911bc2abe8ce9` is unchanged.

The original root Animator, skeleton Transform identities, Face renderer,
Character System modules, activities/animation definitions, speech, emotion,
lip-sync, and flow configuration are retained. Native baked Face geometry and
materials replace the old combined mesh; the new outfit is a separate skinned
renderer. Eight lip-sync entries are matched by shape name. All 73 shapes remain.

The new importer retains the previous character's 52 Humanoid bone mappings.
Its own reference T-pose was calibrated on a disposable copy and independently
validated after saving. All 160 deform bones are mapped to the existing skeleton;
the new meshes' neutral skinning matches the imported source within 0.00048 mm.

All three old HairInstance objects (scalp, brows, lashes), their boundary, facial
surface binding, hair startup warmup, and fallback components are removed from
this prefab. Baked mesh brows/lashes are part of Face. CharacterReadiness remains
with optional hair references cleared and reports ready. There are no Alembic
or old-model dependencies on the saved prefab. The original hair assets remain
on disk for later use.

The current CharacterTesting instance was also updated. Jiwoo remains inactive
until selected, preserving Ethan as the initial selection. The separate loose
`Jiwoo_EthanSetup` preview instance was disabled to avoid a duplicate character.
The original selection buttons retain their configured flow controller objects.

The prefab and importer are saved. CharacterTesting already had unsaved changes;
it remains dirty so those changes are not silently committed. Save the scene
in Unity when ready. `CharacterTesting_working_before.unity` preserves the prior
working scene; `Before` preserves the original prefab/importer and saved scene.

`replacement_report.json` records the configuration-preservation and geometry
checks. `verification_report.json` independently confirms the saved asset and
current scene's mesh/avatar/module references, zero missing scripts, valid saved
T-pose, and no Alembic dependencies. Existing Thinking, Talking, Thumbs Up,
Waving, and Idle clips remain Humanoid animation dependencies. Animation playback
and rendering remain user review; no clips, Play Mode, or builds were run.

Reproducible Editor tool: `Tools/Hair/ReplaceJiwooPrefab.cs`, through Unity CLI's
in-memory `run_script` command. `ConfigureImport`, `Apply`, and `Verify` are the
ordered entry points. Do not rerun Apply over later user edits without review.
