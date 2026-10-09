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

## Matte skin follow-up

Created Skin_Shader_graph_Matte.shadergraph from the user's Skin_Shader_graph graph, retaining all color/normal connections and exposed skin-tone controls. Added exposed local Boolean Shader Feature keywords with shader-build-setting overrides disabled so the generated variants define the exact URP macros:
- Disable Skin Highlights: _SPECULARHIGHLIGHTS_OFF (enabled).
- Disable Skin Environment Reflections: _ENVIRONMENTREFLECTIONS_OFF (disabled).

Assigned Ethan_Head_SkinTone_Matte and Ethan_Body_Matte to the active character. Direct-light specular highlights are disabled on both. Environment reflections remain enabled because the visual comparison removed the objectionable chin/collarbone highlights without disabling them. Hair, eyes, shirt, textures, lighting, and the original graph/material assets were not edited during this follow-up.

Validation: shader messages empty, scene saved, Edit-mode before/after captures, Play-mode visual verification and no new runtime errors.

To reverse this follow-up, assign Ethan_Head_SkinTone and the original Ethan_Body materials to their original Face renderer slots. Screenshots: matte-before.png, matte-direct-off.png, matte-runtime.png.
