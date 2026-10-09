# Portrait rendering pass

Applied in the live Unity Editor to Assets/_Sandbox/Scenes/CharacterTesting.unity. Kept URP, the current camera, and the hair and eye materials.

New material/profile variants are in Assets/_Sandbox/Settings/PortraitRendering. Original Ethan materials and Studio_VolumeProfile are preserved.

Settings:
- Key light: intensity 0.75, shadow strength 0.5 (previously 0.8 and 1).
- Fill light: neutral white, intensity 0.65 (previously cool tint, 0.4).
- Rim light: intensity 0.2 (previously 0.5).
- Flat ambient color: RGB 0.52 (previously 0.43137).
- Head variant: smoothness 0.30, normal strength 0.65 (previously 0.45 and 1).
- Shirt variant: base multiplier RGB 1.65 / 1.65 / 1.60, smoothness 0.15, normal strength 0.65.
- Color grading: exposure +0.15, contrast -5, saturation -8 (previously +0.5, -10, +10).

Screenshots before.png and after.png use the same 805 x 642 Edit-mode camera capture. runtime.png verifies Play mode; animation changes the pose slightly.

Validation: visual review, successful scene save, Play-mode capture, no new runtime errors and no compilation errors. A diagnostic attempt to register RenderSettings for Undo logged a null-object error; the ambient assignment and save succeeded. A saved scene backup provides rollback independently of Undo.

Rollback: stop Play mode, close this scene, copy CharacterTesting.before.unity.txt to Assets/_Sandbox/Scenes/CharacterTesting.unity, then reopen it. This restores the saved state from immediately before this pass, including the user's pre-existing work. Do not overwrite later scene work. New portrait assets may remain unused after rollback.

Remaining differences: standard URP Lit has not been replaced with a skin scattering shader; eye/cornea materials and hair geometry/shading are unchanged. Lighting affects their appearance.
