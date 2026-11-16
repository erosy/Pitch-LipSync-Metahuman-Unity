# Jiwoo corrected rig — Batch 2 Unity integration

The approved Batch 1 bake is installed in the existing
`Assets/_Sandbox/Characters/Jiwoo/Jiwoo_Normalized.prefab` and the
`CharacterTesting` scene. The prefab's original GUID is preserved. The Girl
button still targets Jiwoo's `CharacterFlowController.PlayResponse`; Boy still
targets Ethan. Voice, modules, activities, and shared hair/readiness scripts
remain connected.

## Current assets

- `Jiwoo_Rigged.fbx`: one combined Face mesh, 73 facial keys, and a 160-bone
  deform skeleton derived from the delivered Rigify skin weights. Its valid
  Humanoid avatar is configured explicitly, including fingers.
- `Groom/Rigged`: scalp (100,498 x 64 samples), eyebrows (1,915 x 32), and
  eyelashes (787 x 32), all head-local in meters. They attach beneath
  `DEF-spine.006` with identity local transforms and Fixed strand scale.
- `Textures/Rigged` and `Jiwoo_Outfit.mat`: delivered textures, remapped existing
  skin/eye/mouth materials, and the delivered outfit. The outfit's glTF
  metallic/roughness map was repacked for URP metallic/smoothness.

The Batch 1 review file reversed the facial groom labels. Batch 2 corrects the
labels based on their anatomical heights: `main.001` is eyebrows and
`Group_0.001` is eyelashes. Source groom geometry is preserved.

The export removes authoring controls and stacked armature modifiers, retaining
normalized Rigify deform weights. The 654 eye-edge/cartilage vertices driven
only by the old head rig are attached rigidly to the new head. Their existing
lack of facial deformation remains a review limitation.

## Verification performed

- Independent Blender FBX readback retained 67,216 vertices, all 160 deform
  bones, material order, and all 73 shapes; maximum geometry error 0.00125 mm.
- ABC readback retained all strand samples and intended diameters exactly.
- Unity has 70,031 rendering vertices after seam splits. Comparison of neutral
  geometry and all 73 shapes passed, maximum error 0.00207 mm. All 160 bone
  positions and distributed samples from all three grooms passed their coordinate
  comparisons. Root/head scales are approximately one.
- Saved prefab and scene checks passed: no missing scripts, valid Humanoid,
  complete materials, valid character/module configuration, valid lip-sync
  indices, and preserved Girl/Boy button targets. The current prefab has no
  dependency on the old Jiwoo models or groom assets.
- Unity compilation reported up to date with zero compiler errors or warnings.
  No Play Mode, animation playback, audio playback, runtime simulation tests,
  or builds were run. Edit Mode groom simulation is disabled.

Reports: `blender_readback.json`, `unity_geometry_verification.json`,
`saved_verification.json`, and `compilation.json`. Batch 3 removes the superseded
`Before/` backups and temporary geometry dumps after dependency verification.
To regenerate those dumps for numerical checks, run `InstallJiwooRigged.DumpImport`
before `verify_jiwoo_rigged_unity.py`.

## User review and remaining work

Open `CharacterTesting` and inspect Jiwoo; runtime testing is yours to perform.
Check body animation/retargeting, speech and facial appearance, eye-edge contact,
the previously reported outfit motion, hair rendering, and simulation cost.
The facial poses remain authored approximations. Facial grooms remain
head-attached; expression-driven surface deformation is not implemented by this
batch, and brows/lashes may need further work during facial animation.

Batch 3 removes old Jiwoo assets, experiments, backups, and the hairstyle
selector. The shared character hair/readiness improvements are retained.

Reproducible tools: `export_jiwoo_rigged.py`,
`verify_jiwoo_rigged_export.py`, `InstallJiwooRigged.cs`, and
`verify_jiwoo_rigged_unity.py` under `Tools/Hair`. The exporter refuses to silently
ignore manual edits to the prepared blend: those require refreshed bake
baselines first. Do not re-run the installer on an already replaced scene.
