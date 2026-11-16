# Ethan V2 finger animation repair

The installed `Ethan_V2.prefab` now transfers old Ethan's finger rotations
while preserving V2's authored joint spacing. This repairs the Unity pose
bridge; the facial bake, source FBX, Blender files, geometry, skin weights,
and bind poses were not changed.

The previous transfer treated names such as `index_02_bulge_l` as a
metacarpal. Some thumb helpers fell back to a nearby pinky joint. Independently
transferring world positions also stretched the differently proportioned
finger chains. In the sampled animations, maximum displacement from a
finger/helper's parent-local rest position was 228.16 mm.

The 38 main joints (30 phalanges and eight metacarpals) use bind-relative
driver rotation with fixed destination local positions. The 172 additional
finger skin helpers retain their authored local position and rotation under
their own parent joint. Their source identities are now anatomically named,
instead of guessed by proximity. This is a conservative deformation setup;
it does not recreate MetaHuman's runtime corrective solver.

## Verification

- Driver finger rest transforms match old Ethan's saved hierarchy.
- Eight Humanoid animation definitions, nine samples each: saved prefab
  preserves all finger local positions and helper local rotations. Main joint
  rotations match the old Ethan driver transfer.
- Thirty isolated 60-degree source-joint curls: zero movement in other fingers.
- Neutral body skin difference: 0.000834 mm (floating point tolerance).
- Saved prefab reload and current CharacterTesting instance: 210 repaired
  bindings each.
- Existing character verification passes, including 657 secondary facial
  synchronization checks and the existing lip-sync/animation setup.
- Compilation passed. No Play Mode transition or build was run.

`verification.json` records the before/after measurements;
`saved_verification.json` checks persistence. `Previews` contains disposable
Edit Mode skin-matrix renders of both hands in Waving, Thinking and Thumbs Up,
before and after. These show deformation with a plain material rather than
the character's production shading. The pre-repair prefab is in `Before`.

Tools: `AuditFingers.cs`, `RepairFingers.cs`, and `RenderFingerReview.cs` under
`Tools/EthanV2`. Run `RepairV2Fingers.VerifySaved` for read-only verification.
The original transfer installer also uses the corrected finger mapping/modes.
The scene was already dirty and was left unsaved. Runtime review remains under
the user's Play Mode control.
