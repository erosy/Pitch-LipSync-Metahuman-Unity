# Hierarchy and component wiring

Inspect the current approved prefab, component source and serialized fields before
building. `Data/EthanV2/Revamp/README.md`, `Tools/EthanV2/RevampCandidate.cs` and
`VerifyRevampCandidate.cs` document the native candidate; the older
`TransferCharacterSetup.cs` includes scene mutation and a compatibility skeleton.
Do not call that historical transfer as a harmless component copier.

## Native candidate shape

```text
CharacterRoot
  Armature                         Animator + calibrated native Humanoid Avatar
    ... head
      SpeechGazeFrame              calibrated frame; not a rotated deform bone
      Hair / HeadMeters            actual attachment names may differ
        HeadCollision              HairBoundary / fitted SDF
        ScalpHair                  HairInstance
      Voice                        AudioSource + uLipSync
  Face                             SkinnedMeshRenderer + uLipSyncBlendShape
  Outfit                           SkinnedMeshRenderer
  Modules
    [animation child]              AnimationModule
    [expression child]             ExpressionModule
```

Root components: `Character`, `CharacterSpeechPlayer`, `CharacterFlowController`,
`CharacterHairStartupWarmup`, `CharacterReadiness`; preserve additional current
appearance/fallback components that are actually used. For this project's final
Unity integration, `Armature`, `Face`, `Outfit` and `Modules` are the required
character layout. Module child labels and head-attachment labels are cosmetic.

Consolidate the visible head, body, eyes, teeth/tongue and other mouth sections,
brows and lashes into `Face`, preserving material sections and all expression
deformations. Keep garment geometry in `Outfit`. Dedicated scalp/fallback hair
objects may remain separate when used. Do not leave `BAKED_*` GameObjects,
duplicate export sections, authoring archives or redundant disabled source mesh
objects anywhere in the delivered prefab, including inactive descendants. Simply
renaming the separate bake-export objects or putting them under a parent is not
consolidation. Preserve original bake/source files outside that hierarchy.

| Component | Required relationship to verify |
| --- | --- |
| Character | `modulesRoot` points to its Modules child; `defaultIdle` is valid, idle-tagged, looping animation activity |
| AnimationModule | `animator` points to the one native Animator; preserve activity definitions |
| ExpressionModule | `face`, `lipSync`, `blendShapeDriver` point to this character; debugger only when intentionally used |
| uLipSyncBlendShape | `skinnedMeshRenderer` is Face; map phoneme indices by expression **name**, never copy another mesh's numeric indices |
| uLipSync | persistent `onLipSyncUpdate` callback targets this driver; preserve profile and audio processing configuration |
| CharacterSpeechPlayer | `audioSource` and `lipSync` target Voice; preserve clip/profile policy |
| CharacterFlowController | `character`, `readinessProvider`, `speechPlayer`, greeting/response definitions resolve within this character |
| CharacterHairStartupWarmup | `targetAnimator` is the native Animator; `strandHair` references this groom |
| CharacterReadiness | `startupWarmup` and any used `hairFallback` are current components |

Separate runtime facial meshes are an exception only when explicitly requested
by the user; in that case retain `CharacterFacialMeshSync` with complete named
bindings and verify every shape. Separate brows/lashes in Blender are valid inputs:
preserve their working evaluated deformation, then carry it into the merged Face.
Merging must retain expression deltas; it does not create missing deformation.
Remove obsolete facial synchronization targets after merging, but retain any
binding still needed by Outfit or another explicitly approved runtime mesh. Do not
discard small nonzero body/outfit expression records merely to remove the component.

Copy components using an explicit object-remap table. Remap custom serialized
references and persistent events; never rewrite native `m_GameObject`, `m_Script`
or prefab identity fields. Inspect dependencies for stale references into the old
character. Use disposable prefab contents/preview scenes and verify save success
before deleting temporary objects. No automatic scene selection switch is implied
by a request to prepare a candidate.

## Gaze axes and placement

`SpeechGaze` expects a frame with +Z out of the face and +Y toward the crown.
Native MetaHuman head axes need not satisfy this. Add an empty child frame at the
head with unit local scale and usually zero local position; calibrate its rotation
in neutral, assign `speechGaze.head`, and do not rotate the deform bone to fix it.
Leaving target empty uses `Camera.main`. Validate local yaw/pitch signs at center,
left/right and above/below camera positions before enabling head assistance.

Start eye-only when fixing an axis mismatch. Current code can rotate its assigned
frame for head assistance: rotating a helper child does not inherently rotate the
skinned head, so verify actual head response if that behavior is requested. Do not
claim that assigning an empty child alone implements skeletal head tracking.
The historical code gated tracking on speech. The current `SpeechGaze.cs` also
has `lookAtPlayerDuringIdle`, with expression/motion arbitration: inspect its
Inspector setting before proposing a code change. Idle eye darts or authored
eye poses can intentionally interrupt tracking. Clarify whether the requested
behavior includes idle, speech and authored gaze exceptions. Preserve user edits.
Current gaze also exposes `eyeOnlyDownwardGaze`, using the additional
`cameraEyeLookDownLeft/Right` shapes for eye-only downward tracking. Preserve
these runtime extension shapes if present; the historical 73-shape solver bake
does not imply the final active Face must have exactly 73 shapes. Do not enable
the option on a mesh without checking those shapes and their eyelid behavior.

Check camera distance, root position and framing separately from model scale.
Ethan's small candidate appearance came largely from z=-4.56 versus approximately
-0.51 for existing characters. Those are historical scene values, not defaults.

## Verification and replacement

Run `Character.ValidateConfiguration` and flow validation; check missing scripts,
module duplication, Animator/Avatar validity, renderer bones/materials, all names,
weights and persistent callbacks. Sample disposable neutral, blink, raised brow,
smile and speech combinations, plus Waving/greeting, Talking, Thinking, idle and
fingers. Record numeric preservation and matched visual comparisons separately.
Verify head-following hair/collision and startup/readiness references.

Reopen the saved prefab and enumerate all descendants, including inactive ones.
Reject the delivery if any GameObject name starts with `BAKED_` (case-insensitive), if raw export
sections remain separate, or if expected character renderers are not `Face` and
`Outfit` (apart from used scalp/fallback hair and explicit user exceptions).
Check the actual referenced meshes, section manifest and world-space expression
preservation so a label-only cleanup cannot pass. Remap ExpressionModule, lip-sync
driver, persistent audio callbacks and any mesh/renderer attachments to final Face;
verify no references point to removed sections. A failed hierarchy check means
integration is unfinished, even when facial synchronization already works.

When replacement is requested, wire the actual selection controller, UI button
events and any `VoiceLipSyncDemo` references to the new flow/voice. Preserve the
other character and avoid duplicate selection/speech callbacks. Confirm live scene
and saved prefab references independently; do not overwrite unrelated scene edits.
