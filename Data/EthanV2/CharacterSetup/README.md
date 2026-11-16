# Ethan V2 speech, animation and selection

The saved `Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab` now reuses old
Ethan's Character modules, default idle, speech clip/profile, phoneme mappings,
greeting flow and response flow. The Boy button's existing selection controller
now points to V2; repeated Boy selections retain the existing response behavior.
Old Ethan remains inactive in the scene and its original project assets remain.
The girl selection configuration is preserved.

The scene was already dirty, so it was **not saved**. Save CharacterTesting to
retain the Boy button/selection changes. Prefab configuration is saved. A
before-copy of the V2 prefab and metadata is under `Before`.

## Animation without altering the delivered skeleton

V2's FBX contains three separate skin rigs and a Generic model avatar. The
`AnimationDriver` child contains a non-rendering copy of old Ethan's required
bone hierarchy and uses the existing validated `Ethan_unityAvatar`. The
Animation Module and hair warmup explicitly reference this driver Animator.
The Generic root Animator is disabled intentionally. Existing humanoid clips
are retargeted by the driver; `CharacterSkeletonPoseBridge` applies bind-relative
motion to the separate visible body, face and clothing skeletons.

Finger transfer now preserves V2's joint spacing and attaches finger skin
helpers to their own joints. See [finger repair and verification](../FingerRepair/README.md).

No native V2 bone hierarchy was unpacked/reparented, and no FBX or skin weight
was modified by this transfer. An earlier direct avatar-conversion diagnostic
could not validate the delivered Rigify hierarchy; automatic approval review
rejected unpacking/reparenting it. `PrepareHumanoidAvatar.cs` and its diagnostic
reports are not part of the installed setup and should not be rerun as an
installer. The implemented path preserves the original hierarchy.

`CharacterFacialMeshSync` mirrors the primary head's named blendshapes to the
nine separate eye, teeth, cartilage and inner-mouth renderers after facial
writers run. Brows and lashes remain integrated in the primary head. This
prevents jaw speech/blinks from leaving the secondary facial meshes neutral.

The original eight phoneme mappings are preserved by shape name, including
the old A/jawOpen, I/mouthFunnel and U/mouthPucker tuning. The lip-sync callback
targets V2's blendshape driver. Voice analysis and speech playback share the
copied AudioSource on V2's head. The existing greeting/response phase assets
are reused rather than edited globally.

## Verification

- Compilation completed with zero errors or warnings.
- Character and flow configuration validators reported no errors.
- Disposable Edit Mode PlayableGraph samples verified Waving, Thinking,
  Breathing Idle and Idle, without entering Play Mode or playing audio.
- Full bone-weight skin-matrix evaluation confirmed visible body/clothing motion.
  Applying the neutral bridge changed skinned vertex positions by at most
  0.00108 mm, and body animation preserved the head expression weight.
- All 73 names were checked across nine secondary facial meshes: 657
  synchronization checks passed.
- Lip-sync callback, eight phoneme mappings, valid Humanoid avatar, saved
  readiness references, and Boy selection wiring were inspected.

See `verification.json`, `prefab_transfer.json`, and `scene_transfer.json`.
Runtime timing, greeting-to-response sequencing, speech audio, gaze, body/cloth
contact and scalp settling still need user-controlled Play Mode review.

Tools: `TransferCharacterSetup.cs`, `InstallFacialSync.cs`, and
`VerifyCharacterSetup.cs` under `Tools/EthanV2`. Runtime support is in
`Assets/_Sandbox/Scripts/CharacterSkeletonPoseBridge.cs` and
`CharacterFacialMeshSync.cs`. The transfer refuses duplicate character setup.
