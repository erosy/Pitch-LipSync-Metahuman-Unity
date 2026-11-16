# Ethan V2 facial bake

Production facial hair now uses a lower-density head mesh derivative in
`Meshes/EthanV2_Head_FacialHairOptimized.asset` on the saved V2 prefab. The
original FBX/Blender sources remain unchanged. All strands, roots/tips, taper,
skin weights and 73 shape channels are retained; only longitudinal mesh
tessellation is reduced. Current character mesh triangles are 274,793 instead
of 748,415. See [optimization and checks](FacialHairOptimization/README.md).
Do not restore the dense FBX head onto the production prefab during reimport.

Finger animation now preserves V2's joint spacing while following old Ethan's
driver rotations. Incorrect helper-bone mappings were repaired in the Unity
prefab. See [finger repair and before/after checks](FingerRepair/README.md).

Speech, animation and old Ethan's greeting/response phases are configured on V2.
Boy selection now targets V2, while old Ethan remains hidden. See
[character setup and verification](CharacterSetup/README.md). Scene selection
changes remain unsaved; the V2 prefab configuration is saved.

Scalp groom is now installed in `Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab`.
See [scalp setup and validation](Groom/README.md). Simulation uses 32 points per
strand and 30 Hz; Edit Mode simulation is disabled.

Latest Unity model update: eyebrows and eyelashes are mesh sections inside the
head. See [mesh eyelash handoff](Eyelashes/README.md) and the latest source
`Eyelashes/Ethan_V2_73_WithBrowsAndLashes.blend`. The approved brow-only version
and its [handoff notes](Eyebrows/README.md) remain available for recovery.

The original solver and DNA dependencies are resolved. The completed bake and
restored face-board source are under `Native`.

- [Baked 73-shape character](Native/Ethan_V2_73_Baked.blend)
- [Restored original facial-board source](Native/Ethan_V2_FaceBoard_Restored.blend)
- [Review instructions, method, and limitations](Native/README.md)
- [Saved-file verification](Native/reopen_verification.json)

All 962 mesh/shape comparisons passed without the addon loaded. Maximum
reconstruction error was 0.000835 mm. Visual review remains necessary,
especially inner lips and tongue after restoring DNA correctives to the
modeller's split head. The source Downloads files and active Unity assets
remain unchanged. No Play Mode transition or build was run.

The initial inspection JSON files and previous-baker recovery are historical
records from before the correct Ethan DNA and installed solver were available.
Their earlier missing-dependency conclusions are superseded by Native reports.
