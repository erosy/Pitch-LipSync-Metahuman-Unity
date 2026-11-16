# Ethan V2 scalp groom

Use `Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab`, a variant of the
existing V2 model. The placed V2 instance in CharacterTesting was connected to
this prefab. The scene was not saved; save it to retain that placement/setup.
The original Ethan prefab and scalp asset were not edited.

Source is the scalp object inside the supplied `groom/Hair_Overall.abc`.
Eyebrow, eyelash and peachfuzz Alembics are not installed. The scalp has 94,408
strands, sampled to 32 points while preserving every root and tip. Its original
ABC-to-Blender correspondence is within 0.00188 mm after coordinate registration.
The Unity provider/build preserved every sampled endpoint exactly.

The custom Hair Asset and BlenderFittedHairData provider use the same pipeline
as old Ethan. The V2 strand material and old Ethan physics/render settings are
used, with Fixed strand scale, 30 Hz simulation and Edit Mode simulation off.
There is one scalp instance. The source and latest facial mesh assets are
unchanged; mesh brows and lashes remain in the head.

The model's native head bone has a 100x import unit scale. `EthanV2_HeadMeters`
cancels that inherited unit scale, keeping the groom's coordinates in meters.
The Hair Instance beneath it has zero local translation/rotation and unit local
scale. A disposable Edit Mode 15-degree head rotation checked that its world
scale remains one and that collision and hair follow the same head frame.

`EthanV2_HeadSDF96.asset` is the active 96-cubed RFloat collision field, fitted
to the current neutral head, with a 1.5 mm solid margin. Blender-to-Unity
handedness is accounted for in the signed-distance winding. A CPU sign check
confirmed a negative distance inside the skull and positive distance above it.
This is a rigid neutral-head collision proxy, not a facially deforming field.
The initial 64-cubed generated texture remains unused; the prefab references
the corrected 96-cubed asset.

CharacterHairStartupWarmup and CharacterReadiness are wired on the root.
No runtime Play Mode test or build was performed. Review startup reveal,
settling, head turns, clipping and frame time in user-controlled Play Mode.
No matching non-compute hair-card fallback is added by this integration.

Reports: `export.json`, `sdf_sign_check.json`, `installation.json`.
Tools: `export_scalp32.py`, `InstallScalpGroom.cs`, `VerifyScalpGroom.cs` under
`Tools/EthanV2`. The installer refuses duplicate scalp assets/instances.
