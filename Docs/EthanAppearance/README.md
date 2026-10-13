# Ethan appearance pass

Implemented in `Assets/_Sandbox/Scenes/CharacterTesting.unity` using Unity 6000.5.9f1 / URP 17.5. Neutral is the saved default. WebGL builds, browser performance, and Boy/Girl switching were deliberately not tested.

## Visual results

- [Baseline / Neutral / ACES comparison](Comparison.png)
- [Full-resolution selected Neutral portrait](Neutral.png)
- [Neutral face detail](Neutral_Face.png)
- [Matched-brightness ACES alternate](ACES.png)
- [Play-mode smile, blink, head-turn, and speech frames](Motion/ContactSheet.png)

All three main comparison images use the same 1920 x 1080 Game-view framing and Edit-mode pose. The runtime checks use a fixed sample of the existing animation for repeatable visual inspection. The supplied Cycles JPEG is a look reference, not a pixel-registration target: its pose, camera, geometry/shading, and AgX view transform differ.

## Selected settings

| Setting | Final value |
|---|---|
| Camera HDR / HDR display output | Enabled / disabled (SDR output) |
| URP grading / LUT size | HDR / 32 |
| Neutral Volume | Exposure 0, contrast 0, saturation -10; neutral white balance/filter |
| ACES alternate | Exposure +0.255 EV; otherwise the same grade |
| Key light | Intensity 1.0; rotation (0, 180, 0); white; temperature disabled |
| Key shadows | Soft, strength 0.35; light bias 0.05, normal bias 0.2 |
| Left fill | Intensity 0.55; rotation (10, 90, 0); no shadows |
| Right fill | Intensity 0.50; rotation (10, 270, 0); no shadows |
| Flat ambient | RGB (0.35, 0.35, 0.35) |
| Main shadow map / filtering | 2048 / medium |
| SSAO | Intensity 60, direct-light strength 1, radius 0.006, falloff 60, High samples (12), High bilateral blur |
| Head and body | Original textures, white base tint, normal strength 0.8, smoothness 0.5; direct highlights/environment reflections disabled |
| Shirt | Base multiplier (1.25, 1.25, 1.15), smoothness 0.10; existing normal strength 0.65 |
| Eyes | Opaque Complex Lit, specular workflow; base specular RGB 0.001; smoothness 0.90; normal strength 0.50 |
| Eye clear coat | Mask 0.25, smoothness 0.98; specular highlights and environment reflections enabled |
| Studio reflections | Offline-baked 256 cubemap; intensity 1; head-height probe; no runtime capture |

The starting lighting values were visually too dim after removing the original +1 EV exposure. The final key/fill/ambient values restore facial brightness without lifting the entire background. No orange global filter or new scattering shader was needed. Skin textures retain local red variation; the reference Blender materials have subsurface weight zero and very low specularity.

Neutral and ACES cheek samples measure approximately 150.94 and 150.87 on a 0–255 display-luma scale at matching exposure. These are brightness-matching diagnostics, not a perceptual color-accuracy score. ACES has stronger contrast and a different color response; Neutral remains selected for the softer portrait.

The studio map is produced from three white, HDR unlit softbox cards on a temporary isolated capture layer. Ethan is excluded. Capture cards are destroyed after baking, so there are no extra cards or layer-name changes in the saved scene. The probe uses **Custom** mode with `customBakedTexture` pointing to the baked EXR. Unity's standalone `BakeReflectionProbe` assignment to `bakedTexture` did not survive scene reload without scene lighting data; the explicit custom slot fixes persistence. This is still a static, precomputed reflection, not a realtime probe. `BakeEyeStudio.cs` recreates the map if needed.

## Jaw-side shading follow-up

The narrow dark streaks between Ethan's cheeks and ears were form shading from front-clustered lights, amplified by SSAO. In controlled Game-view captures, turning off key shadows or the head normal map did not remove them. Turning the two shadowless fills farther toward the sides softened the streaks; halving the SSAO radius reduced their remaining width. The saved fill rotations are now (10, 90, 0) and (10, 270, 0), and the shared renderer's SSAO radius is 0.0125. `JawDiagnosis2/` and `JawDiagnosisRadius/` contain the comparisons; `JawSoftened.png` is the earlier saved result. The original wider streak is in `JawDiagnosis2/Current.png`.

To undo only this follow-up, restore the Left Light Y rotation to 150, Right Light Y rotation to 250, and SSAO radius to 0.025. Leave the other appearance-pass settings as they are. `FinalSettings.json` records the initial appearance pass, before the later user lighting edits and this follow-up; the table above records the current scene.

## Neckline SSAO follow-up

The user's later SSAO setup used intensity 60, direct-light strength 1, Low sampling (4 samples), Medium Gaussian blur, radius 0.0125, and falloff 60. It produced a fuzzy dark outline along the neck and shirt opening. Matched Game-view captures in `NeckDiagnosis/` show that changing to High sampling and High bilateral blur alone left intensity 60 too harsh. Intensity 5 with High sampling and blur removed the fuzzy outline while keeping direct-light strength 1 and some collar contact shading. `NeckCurrent.png` and `NeckSmoothed.png` are the before/after captures. Only the shared renderer asset was saved during this follow-up; the user's unrelated unsaved scene edits remain open in the Editor.

The user preferred the darker hair produced by intensity 60. A later comparison kept intensity 60 and direct-light strength 1, then narrowed the AO radius to 0.006. This removes the broad fuzzy neckline band while retaining some dark hair occlusion. The scene uses the original procedural `Ethan_StrandHair` material; a trial Face hair-slot variant did not affect the procedural strands and was removed. `AO60Diagnosis/`, `AO60Fine/`, and `HairAO60_Final.png` document the comparison and final Game view. Reducing radius does make the strand hair less dark than intensity 60 at radius 0.0125; no separate strand-color change was saved.

To undo only the latest AO-radius choice, restore radius to 0.0125 in `Assets/Settings/URP/URP_Renderer.asset`. The shared renderer affects other scenes using it, so inspect their Game views if this renderer is reused.

## Assets and tools

New materials, profiles, and cubemap live under `Assets/_Sandbox/Settings/EthanAppearance/`:

- `Ethan_Head_CyclesMatch`, `Ethan_Body_CyclesMatch`, `Ethan_Shirt_CyclesMatch`, `Ethan_Eyes_ClearCoat`.
- `Ethan_Neutral.asset`, `Ethan_ACES.asset`, `Ethan_StudioReflection.exr`.

The scene overrides Ethan's face slots 0/1/3/4 and the outfit material. Source materials remain intact. Teeth, cartilage, hair assets/materials, camera transform/FOV, lip-sync and expression source, and character switching code are untouched. Eye MotionVectors are enabled on the new material. The new probe is named `Ethan Studio Reflections`; the Face renderer's probe anchor points to it.

The existing shared URP asset changes color grading from LDR to HDR. The shared renderer carries the SSAO settings listed above. These shared settings can affect other scenes/characters using them; this pass visually validates Ethan only.

`Tools/Appearance` contains Editor-evaluated implementation and validation scripts. They are outside Assets and introduce no runtime scripts or public APIs. To reproduce from the preserved baseline: run `Prepare.cs`, `Apply.cs`, `BakeEyeStudio.cs`, `Calibrate.cs`, then `SaveSelection.cs` using `unity command eval_file --caller plugin --skill urp-postprocessing --file <path>`. Apply/Calibrate reset this pass's values; do not rerun over subsequent artistic edits without reviewing them.

To inspect the alternate, assign `Ethan_ACES.asset` to Studio Volume's Profile. Assign `Ethan_Neutral.asset` to return. Both use the same materials and lighting; no double tonemapping or LUT is involved.

## Validation

- Saved and reopened the scene; verified HDR, SDR output, Neutral overrides, eye coat keywords, material assignments, reflection texture/anchor, and absence of capture cards.
- Compiled shaders report no messages. Final Editor console checks report no compilation or runtime errors.
- Captured 20 Play-mode frames across neutral, smile, blink, head-turn, and speech scenarios. Static visual poses temporarily suspend the expression/animation drivers; the speech case restores the actual ExpressionController and uLipSync drivers and plays Ethan's existing audio directly, without invoking character switching.
- Speech captures record playing audio and changing blendshape weights. Blink captures show closed lids correctly occluding eye highlights; sampled head-turn frames show coherent eye reflections and continuous facial shading. No new eye clipping or obvious highlight instability was observed in the sampled frames.
- Scene reload validation exposed and fixed the reflection persistence issue described above; motion validation was repeated with the persistent custom cubemap.
- Existing fine hair/facial-hair aliasing remains visible at close range. This pass does not change its geometry, simulation, or shading, and sampled screenshots do not establish artifact-free behavior at every viewing angle.

Machine-readable records: `Baseline.json`, `FinalSettings.json`, `ImageMetrics.json`, `Verification.json`, `ConsoleValidation.json`, and `Motion/Validation.json`.

## Targeted rollback

Stop Play mode. Preserve any later work before reverting. To undo only this pass in the Editor:

1. Assign the original `Ethan_Head` and `Ethan_Body` materials to Face slots 0 and 1, and `Ethan_Eyes` to slots 3 and 4. These source materials are in `Assets/_Sandbox/Characters/Ethan/Materials/`.
2. Assign `Assets/_Sandbox/Settings/PortraitRendering/Ethan_Outfit_Portrait.mat` to Outfit. Restore Face's probe anchor to None and remove `Ethan Studio Reflections`.
3. Assign the preserved `PortraitRendering/Portrait_VolumeProfile.asset` to Studio Volume.
4. Set camera HDR off, allow HDR display output on, and URP grading to LDR. Restore SSAO intensity 0.5, direct-light strength 0.25, radius 0.03.
5. Restore ambient RGB 0.2901961. Restore key intensity 0.55, rotation (5,160,0), shadow strength 0.2, normal bias 0.4, and temperature enabled at 6536 K. Restore both fills to intensity 0.5 and rotations (0,100,0)/(0,260,0). They remain white and shadowless.
6. Save the scene and modified pipeline assets. Unused new appearance assets can remain for comparison.

`Rollback/CharacterTesting.before.unity` is a copy of the actual pre-pass scene, including pre-existing user edits. The two `.asset.txt` files preserve the pre-pass pipeline/renderer. They are reference snapshots; replacing current files wholesale would also erase subsequent work. Prefer the targeted restoration above or review a diff against these snapshots.
