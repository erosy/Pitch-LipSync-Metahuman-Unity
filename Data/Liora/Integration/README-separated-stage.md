# Liora groom and character candidate

Delivered 18 November 2026 in Unity 6000.5.9f1.

Open `Assets/_Sandbox/Characters/Liora/Candidate/Liora_Candidate.prefab`.
This is a separate candidate built from the user's current `Liora_73_Baked.fbx`,
using the inspected current EthanV2_Candidate as the component/API reference.
The existing scene instance and selection UI have not been replaced. Drag the
candidate prefab into a scene for review; Play Mode remains user controlled.

## Implemented hierarchy

```
Liora_Candidate
  Character, CharacterSpeechPlayer, CharacterFlowController
  CharacterHairStartupWarmup, CharacterReadiness, CharacterFacialMeshSync
  Armature                         one Animator, calibrated native Humanoid Avatar
    root / pelvis / ... / head
      Hair
        HeadCollision              fitted 96^3 discrete SDF
        ScalpHair                  DemoTeam HairInstance
      Eyebrows                     skinned adaptive strand mesh, 73 expressions
      Eyelashes                    skinned adaptive strand mesh, 73 expressions
      Voice                        AudioSource, uLipSync, copied microphone policy
      SpeechGazeFrame               +Z toward audience, +Y toward crown
  BAKED_head_lod0_mesh              primary facial renderer and lip-sync driver
  remaining baked mesh sections    synchronized by expression name
  BAKED_Outfits                    preserved garment geometry on native helpers
  Modules
    Animation                      AnimationModule
    Emotion                        ExpressionModule
```

The 13 source mesh sections retain their base geometry, topology, material slots,
all 73 expression names/order and every original position/normal/tangent expression
array. Skeleton consolidation remaps facial torso/head joints to the native body,
retains facial deformation/helper bones and adds explicit anatomical clothing
helpers. Each renderer gets compensated bind poses; there is one native deform
skeleton and one Animator. The Avatar uses a disposable meter-based T-pose
calibration, leaving authored neutral geometry unchanged. There is no legacy pose
bridge or duplicate Rigify animation skeleton in this candidate.

## Hair and facial-hair production pass

The scalp uses the delivered working Blender groom (100,477 strands), resampled
by arc length to **32 points**, preserving every root and tip. Simulation is
**30 Hz**, Fixed strand scale, with Edit Mode simulation disabled. It has a fitted
head/ear/neck SDF and an explicit resident boundary; automatic collider capture is
disabled. Hair and collision are parented below the actual mapped head in meters.
The original ABC files and source Blender curves remain untouched; this pass did
not substitute an independently aligned Alembic groom for the working Blender one.

Coordinate conversion was selected from signed axis permutations by matching
Liora's actual imported head to evaluated Blender head geometry. Maximum sampled
correspondence distance: 0.001246 mm. Head frame and groom data were measured for
Liora rather than assumed from Ethan. Float32 root/tip conversion error is below
0.000009 mm. Provider/build endpoint error is zero; final saved candidate world
endpoint discrepancy is below 0.000412 mm.

Brows/lashes were sampled from their delivered working surface-deform bindings
for neutral and all 73 expressions, then converted to adaptive triangular tubes.
They remain separate skinned meshes with matching expressions, synchronized to the
primary face. No re-rigging or merge was required. Centerline selection considers
all sampled expressions, including blink and brow poses, and retains roots/tips.

| Mesh | Strands | Rings/strand | Vertices | Triangles | Max centerline error | Unity mesh memory |
|---|---:|---:|---:|---:|---:|---:|
| Eyebrows | 1,593 | 3-8 | 24,063 | 38,568 | 0.0750 mm | 45.09 MiB |
| Eyelashes | 816 | 3-7 | 11,190 | 17,484 | 0.0750 mm | 14.69 MiB |

Combined: **35,253 vertices / 56,052 triangles**, approximately **59.78 MiB** of
Unity mesh memory including geometry and sparse expression payload. Dense
position-only expression allocation would be 20.10 MiB for brows and 9.35 MiB for
lashes (29.45 MiB combined); dense positions plus normals would be 58.90 MiB.
These figures exclude the scalp simulation. Full mesh figures are in delivery.json.

The source curve radius attribute is uniformly 0.5, an Alembic authoring placeholder
rather than a usable physical strand-width prescription. Candidate widths follow
the established visible production widths: brows 0.25 mm root diameter, lashes
0.12 mm root diameter, with smooth taper to 15% at the tip. This is an explicit
production interpretation, not a claim of recovering unavailable original taper.
Original radius metadata and curves remain preserved in Blender. Approved mesh
brow/lash materials were cloned into Liora-owned candidate materials. Liora's
existing scalp material and model materials were preserved.

The original 2,144-vertex/1,722-triangle eyelash-card renderer is retained with its
expressions and skinning but disabled in this candidate to avoid overlap with the
new strand lashes. Authoring/control geometry stays outside the production prefab.
Body/outfit expression records were audited and retained: small nonzero position,
normal and tangent values mean they were not proven redundant. No body masking or
broad shape/tangent deletion was performed.

## Exact wiring

- Character.modulesRoot -> this candidate's Modules; default Idle uses the current
  reference's valid looping idle activity.
- AnimationModule.animator -> Armature's single native Animator.
- ExpressionModule.face/lipSync/blendShapeDriver -> this candidate's head/Voice/driver.
- uLipSync persistent OnLipSyncUpdate -> this candidate's uLipSyncBlendShape driver.
  Eight phonemes were resolved by shape name, preserving current reference tuning.
- Voice profile -> `Assets/_Sandbox/LipSync/Profile_Liora_ID.asset`.
- CharacterSpeechPlayer clip -> `Assets/_Sandbox/Audio/liora.mp3`; AudioSource and
  uLipSync point to Voice. Audio does not play on awake.
- Flow controller -> this Character, readiness and speech player; current shared
  greeting/response definitions were retained.
- Hair warmup -> the native Animator and Liora's scalp; readiness -> this warmup.
- CharacterFacialMeshSync -> primary head and all 14 secondary shaped mesh sections,
  including the brows/lashes and preserved disabled cards.
- Gaze -> calibrated helper, optional main-camera target. Eye-only tracking starts
  enabled; head assistance is disabled. Reference idle tracking preference remains
  false. Ethan's eye-only-downward extension is disabled because Liora has the
  original 73 shapes, without cameraEyeLookDownLeft/Right. No extra gaze shapes were
  silently copied or invented. This helper does not claim to rotate the skinned head.

No selection controller, UI event, VoiceLipSyncDemo, existing character prefab,
shared activity definition or existing material was changed.

## Verification and visible limits

`static_verification.json` passes saved-asset geometry/expression array comparison
for all 13 original mesh sections, exact saved position/normal records for the two
new facial meshes, 1,022 secondary synchronization checks, no missing scripts,
no stale references into the template character, valid Avatar and configuration,
lip-sync persistent callback, gaze axes, head/collision attachment and scalp layout.
Tiny deltas dropped by Mesh.AddBlendShapeFrame were restored through staged,
strictly ascending sparse records and independently checked through Unity's dense
API arrays. Temporary tangent sentinels used for staging are absent from delivery.

`motion_verification.json` passes 40 disposable Edit Mode samples across Breathing
Idle, Confident Idle, Idle, Nod, Talking, Thinking, Thumbs Up and Waving, plus all
30 finger mappings. This proves mapped motion and bounded finite skinning; it does
not certify garment clearance or runtime hair behavior.

[Previews](Previews) contains neutral, blink, raised brow, joy, speech, three-quarter,
Waving, Talking and Thinking static renders. Neutral/blink/brow/three-quarter/joy
and Waving were visually inspected. The review scalp is a static ribbon proxy
using one fifth of authored strands, with all 32 points; this can look stippled
and does not represent final simulated strand shading or change production density.

**Known review items:** Waving exposes a chest/armhole shirt breakthrough. Full
strength Joy also shows dark eyelid/nose/mouth expression shading inherited from
the source. These remain dedicated garment-fitting and shading tasks. The candidate
is suitable for continuing integration and review, not declared production/runtime
accepted. Hair settling, startup readiness timing, moving collisions, voice audio
and live gaze still require user-controlled Play Mode verification.

All tool scripts compiled in memory; no new runtime scripts were installed. Final
console check reported no compilation failure and no new error entries since the
initial cursor 411. One pre-existing Editor error and two warnings remain; the
console was not cleared. No WebGL build, Play Mode transition or scene save occurred.
The initially dirty CharacterTesting scene remains dirty and its roots unchanged.

## Preservation and reproducibility

The user's current source FBX and importer metadata were copied exactly into
[Preserved](Preserved) before construction. Final SHA-256 checks prove both project
source files unchanged. Source Downloads files, textures, original grooms and the
validated baked Blender file remain unchanged. This candidate's assets live under
Liora/Candidate and new groom assets under Liora/Groom. To roll back this task,
remove only the new candidate and its newly generated groom assets through Unity;
existing source model, materials, scene and character selection remain independent.

Tools, in order: InspectIntegration.cs, ExportHeadFrame.cs,
export_production_grooms.py, BuildCandidate.cs, AttachGrooms.cs,
preserve_facial_records.py, WireCandidate.cs, VerifyCandidate.cs (Static and Motion),
RenderCandidate.cs and FinishCandidate.cs, under Tools/Liora. Tools refuse rebuilding
existing outputs; preserve an existing candidate before a new generation.

Skills/references consulted: integrate-metahuman-character's hierarchy-and-wiring.md,
grooms.md and bake-and-fbx.md; using-unity and unity-cli.md with
references/common-workflows.md and references/integration-advanced.md. Historical
Ethan tools were inspected as examples and adapted to Liora's current assets.
