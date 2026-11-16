# Jiwoo production handoff

## Current assets

- Prefab: `Assets/_Sandbox/Characters/Jiwoo/Jiwoo_Normalized.prefab`.
- Model: `Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx`.
- Scalp Hair Asset: `Assets/_Sandbox/Characters/Jiwoo New/Groom/Jiwoo_Scalp_32.asset`.
- Scalp source: adjacent `Jiwoo_Scalp_32_HeadLocal.abc`.
- Strand material: `Assets/_Sandbox/Characters/Jiwoo/Materials/Jiwoo_StrandHair.mat`.
- Head collision: `Assets/_Sandbox/Characters/Jiwoo New/Groom/Collision/Jiwoo_HeadSDF.asset`.
- Latest editable model: `Data/JiwooRigged/ClothingWeightRepair/Jiwoo_EthanSetup_WeightsFixed.blend`.

Original modeller delivery, prepared Blender sources and repair backups remain
outside Assets. The prefab GUID, Face/Outfit bindings, Humanoid avatar, activities,
expressions and lip-sync references are preserved.

## Approved scalp setup

Use **32 points per strand and 30 Hz simulation** for future character hair,
unless the user explicitly requests another configuration. This preference also
lives in the root `AGENTS.md` and [groom workflow](CharacterGroomWorkflow.md).

Jiwoo has 100,498 strands / 3,215,936 particles. Every root and tip matches the
original 64-point groom; strand diameter is 0.1 mm. Independent Alembic readback
and Unity geometry/reference checks passed. The user approved the improved
frame rate and scalp collision in Play Mode. No numeric FPS gain was measured.

The single scalp Hair Instance is under `DEF-spine.006`, with identity local
transforms and Fixed strand scale. Length/diameter overrides are disabled.
Simulation uses two substeps, eight constraint iterations and a maximum of two
steps per rendered frame. The volume grid is 32 cubed. Edit Mode simulation is
disabled. Eyebrows and eyelashes are mesh sections inside Face.

The 96-cubed RFloat head SDF follows the current head mesh with a 1.5 mm solid
margin; neck and both shoulders retain their capsule boundaries. Automatic scene
collider capture is disabled. All four boundaries follow the current skeleton.
The SDF is a rigid neutral-head proxy, not a facially deforming collision mesh.

`CharacterHairStartupWarmup` references the current Animator and scalp instance;
`CharacterReadiness` waits for its eight-frame pose warmup, reset, and three-frame
settle/reveal. See [shared support](CharacterHairSupport.md).

## Preserved model repairs

The FBX imports authored blend-shape normals using Import, including blink
normals. Keep this setting: switching to calculated normals reintroduces blink
shading artifacts. The current Face has 73 shapes and mesh eyebrows/lashes.
The skeleton retains 160 deform bones and the 52-bone Humanoid mapping.

Clothing seam and skin-weight repairs are baked into the current model/source.
Some garment/skin intersections remain for artistic review during motion.
Do not restore old full-character installers or replace the current model with
the previous `Jiwoo_Rigged.fbx` integration.

## Platform and maintenance

This scalp setup targets Windows strand rendering. Existing compute-support
guards prevent unsupported strand initialization. Jiwoo still needs a matching
hair-card/mesh fallback before website deployment; Ethan's hairstyle is not a
substitute. Runtime buffer-reset reuse and guide clustering remain possible
future optimizations if needed.

For read-only saved-prefab validation in Edit Mode:

```powershell
unity command run_script --file Tools/ValidateProductionHair.cs --entry ValidateProductionHair.Run --caller plugin --skill unity-pipeline --format json
```

The validator checks the 32-point/30 Hz setup, materials, boundaries, character
configuration and missing scripts. It does not save scenes or change Play Mode.
The user controls Play Mode. Routine verification uses compilation and Edit Mode
checks; builds require an explicit request.

One-off authoring/repair/audit tools and obsolete review documents were retired
on 11 November 2026. Their recovery archive and removal manifest are under
`Data/ProductionCleanup/2026-11-11`. Existing model sources, numerical reports,
backups and historical provenance remain in `Data/JiwooRigged`. Active character
assets were preserved byte-for-byte during cleanup.
