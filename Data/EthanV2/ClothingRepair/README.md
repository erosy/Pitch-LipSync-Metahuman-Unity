# Ethan V2 clothing repair — 2026-11-16

Final setup after the user's clarification: the missing shirt was caused by changing its root bone to the 100x body rig while retaining incompatible culling bounds. The shirt now uses the character root as its culling frame with explicit meter-space bounds. The user authorized hiding covered body geometry; the original `EthanV2_Body_ShirtCoverage` variant is active, with the full original FBX body preserved. The static `EthanV2_Outfit_BodyBound` mesh is active. Experimental sculpted body/garment variants and surface-deformation components are not attached or used by the prefab.

`final_scene_verification.json` confirms the open scene inherits this setup, the shirt is enabled, both meshes use the same bones, and old Ethan remains hidden. `FittedRenders` contains Unity depth-tested front/back mesh snapshots of Idle, Waving and Thinking using the outfit's existing double-sided rendering setting. These demonstrate the clothing mesh and covered-body variant, rather than final textured Play Mode playback. Character configuration, expression synchronization and lip-sync checks pass. The prefab is saved; the already-dirty scene remains unsaved. Play Mode was not changed.

The capture shows body skin penetrating the shirt sleeves, rather than disconnected shirt seams. The shirt used a separate Rigify skeleton from the MetaHuman body.

The prefab now uses a cloned shirt mesh bound to the body's existing bones. Weights were interpolated from the closest body triangle, normalized to four influences, with shared weights at coincident garment seam vertices. Neutral geometry, materials and the 73 neutral outfit expression channels are preserved. The original FBX and skeleton hierarchies are unchanged.

A separate cloned body mesh omits 11,676 of 60,816 triangles covered by the shirt in the neutral pose. Coverage uses outward body-normal ray intersections with the shirt, retaining one boundary ring. This avoids remaining hidden-surface clipping during bends while retaining the original body vertex data, skinning and shapes. This body variant is intended for this shirt; restore the FBX body mesh when changing outfits.

`Before/Ethan_V2.prefab` is the pre-repair prefab backup. Source mesh assets remain in the FBX. The installed variants are `EthanV2_Outfit_BodyBound.asset` and `EthanV2_Body_ShirtCoverage.asset` in the character's Meshes folder.

Validation: 16 disposable Edit Mode samples across Breathing Idle, Idle, Thinking and Waving. `comparison.png` shows front projections of representative before/after mesh poses with coverage. Neutral geometry change is below 0.001 mm. Body-relative garment distance change at the 95th percentile improves from 11–46 mm to 2.7–5.6 mm. Coincident seam gaps remain zero. Local edge stretching still occurs around shoulders; this is linear skinning, not cloth simulation. The comparison is geometric and does not substitute for final textured playback.

Character configuration and facial synchronization checks pass (657 checks across nine meshes); lip-sync callback and eight phonemes remain configured. The open CharacterTesting scene inherits both repaired meshes and old Ethan remains hidden. The prefab is saved; the already-dirty scene was not saved. No Play Mode transition or build was performed.
