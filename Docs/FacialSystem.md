# Modular character activities and facial animation

`Sandbox.CharacterSystem.Character` coordinates body and expression playback.
Configuration lives in reusable ScriptableObject assets; playback state belongs
to each character instance. Odin supplies the definition and module Inspectors.

```text
Character root                       Character, existing Animator/model/voice
└── Modules
    ├── Animation                    AnimationModule
    └── Emotion                      ExpressionModule
```

The Animator may be on the root or within the model hierarchy. Each module
references the character's actual Animator or Face. Ethan, his backup prefab,
and both sample prefabs use this setup. The legacy ExpressionController,
silent/speaking lookup types, string expression commands, old scheduler, preset
factories, validation, migration helper, and unreferenced project Animator
Controllers have been removed. Third-party tools and assets remain intact.

## Define and command activities

Use **Create → Character System** to create definitions. Every definition has a
stable `Id` and `DisplayName`. IDs must be unique within their definition type;
renaming an asset does not change its ID.

- **Animation Definition:** skeletal clip, authoritative Loop, Playback Speed,
  and Crossfade Duration. Defaults are speed 1 and crossfade 0.2 seconds.
- **Expression Definition:** one pose of blendshape names/weights and its fade
  duration. Joy and Speaking Joy are separate assets. Shape selectors use the
  shared `Resources/CharacterSystem/BlendShapeDatabase`; runtime binding always
  uses the assigned Face. The canonical shape set is required; extra mesh shapes
  are allowed and left untouched.
- **Activity Definition:** optional animation and expression references plus
  `IsIdle`. Missing animation uses the character's default idle body; missing
  expression fades to neutral. Both references may be empty.

Assign Character's Modules Root and Default Idle. Default Idle must have
`IsIdle = true`, a looping animation, and no expression. Modules are discovered
under the assigned root, including inactive children; duplicate module types
are invalid. Optional absent modules do not prevent the others from operating.

Use `Character.PlayActivity(activity)` to replace the current activity and
`Character.ReturnToIdle()` to reset it. Commands return a bool indicating
acceptance. `CurrentActivity` is read-only, and `TryGetModule<T>(out module)`
exposes initialized modules. Replaying the same activity restarts it. Invalid
commands are rejected before replacing output. Initialization/re-enable starts
the character's default Idle.

Only an explicitly supplied non-looping animation completes an activity and
returns the whole character to default Idle. Expression-only activities persist
until a caller replaces them. Interrupted requests cannot finish newer requests.
Modules receive the same activity/request ID and do not command each other.

## Supplied assets and tuning

Assets are under `Assets/_Sandbox/Data`.

| Activity | Body | Face | Completion |
|---|---|---|---|
| Idle | Preserved character idle, looping | Neutral | Until replaced |
| Idle happy | Preserved character idle, looping | Joy | Until replaced |
| Speaking happy | Default idle fallback | Speaking Joy | Until replaced |
| Thumbs up | Standing Thumbs Up, once | Joy | Whole activity returns to Idle |

The woman has separate Idle, Idle happy, and Speaking happy activities. Her body
retains Breathing Idle; Ethan and the male sample retain their authored Idle
clip. Silent Joy and Thumbs up are shared. The woman has lighter Speaking Joy
weights (smile 35, cheeks 20, eyes 25, inner brow 10); Ethan/male use 55/50/30/10.
The existing Speaking Joy GUID and `emotion_speakingJoy` ID remain stable.

Expressions fade over the preserved 0.18 seconds. Module intensity remains 1;
the allowed range is 0–1.5 and expression output can exceed 100. Speech grace is
0.15 seconds for Ethan/male and 0.1 for the woman. Original five-motion tuning,
uLipSync references, phoneme mappings, profiles, and persistent events remain.
Both sample prefab roots are intentionally saved inactive.

## Body playback and timing

AnimationModule uses a PlayableGraph/mixer targeting the existing Animator and
avatar. Definition Loop overrides the source clip's import setting in either
direction. Module-controlled time wraps loops and completes one-shots; source
clips and import settings are not modified. Interrupted crossfades start from
the current blended output, and superseded playables are disposed.

Body clips may animate Transform and Animator properties; facial/component
curves and object-reference curves are rejected. Production Animator Controller
assignments are empty and root motion is disabled. Normal body, expression, and
random-motion playback uses scaled gameplay time. Speech grace and preview
timing use unscaled time. Movement, facial clips, queues, and random body
gestures remain future work.

## Random facial motions and ownership

ExpressionModule owns expressions, voice-eligibility state, and random facial
motion scheduling. Playing audio on its configured uLipSync AudioSource (or
proxy) plus the speech-end grace period suppresses idle mouth/gaze motion.
Speech state affects eligibility; it never selects a different expression.

Facial Motions are serialized module settings. Each motion has a name, Enabled,
channel, Idle Only, interval, ease-in, hold range, ease-out, variants, and repeats.
One motion runs in a channel at a time; different channels can overlap. Eligible
motions in the same channel are selected uniformly. A variant is a whole pose;
one strength multiplier is sampled for all its shapes, keeping paired eyes
synchronized. Target names also support manual entry.

Sequences run Waiting → Easing In → Holding → Easing Out. Optional repeats
insert a Repeat Gap and reuse the selected variant, strength, and timing. Repeat
probability is rolled once per sequence; a success adds the configured count,
bounded to 0–32. The default double blink uses probability 0.15 and one extra
repetition. Loss of eligibility cancels output; resumption starts a fresh wait.

Blink does not require Idle, silence, or a settled expression. Mouth/gaze motions
require an Idle-tagged activity, a settled expression, and no speech/grace.
Idle happy permits nonconflicting motions after its expression settles.

Expression target shapes and nonzero outgoing expression shapes take priority
over conflicting motion variants. A conflict cancels the entire variant, so one
eye of a paired blink cannot remain active alone. Missing/duplicate/reserved
targets disable whole variants. Cross-channel shape conflicts disable affected
motions; variants within one channel may share shapes. Motion output is clamped
to 0–100.

uLipSync owns every `viseme_` shape and every configured driver mapping, including
mappings with other names. Expressions targeting those shapes are rejected.
Neither facial preview nor normal expression/motion output clears, writes, or
restores visemes. Renderer rebinding and cleanup respect the current ownership
set. Configuration Warnings identify bad references, shapes, tuning, and motion
conflicts without rewriting authored values.

## Independent previews

Select either module's **Debug / Preview** group. Previews work in Edit Mode
without starting the game, and in Play Mode after you start it yourself.

- **Animation:** choose a definition, Preview, Pause/Resume, scrub normalized
  time, and Stop / Restore. A non-looping preview holds its final pose.
- **Expression:** choose a definition, Preview, Preview Neutral, and
  Stop / Restore. The pose fades in and holds; Neutral fades owned expression
  shapes to zero.

Previews leave `Character.CurrentActivity` unchanged, do not command other
modules, and never complete activities. Body and expression previews can run
together. Expression preview suspends random facial output while preserving
uLipSync visemes. In gameplay only the previewed module's activity output and
progression pause. Stop restores its current activity output; body playback
resumes from its suspended time. A new activity cancels previews.

Edit Mode preview captures/restores affected transforms and weights. Previews
stop and restore on disable/destruction, assembly reload, save, scene/Prefab
Stage closing, editor exit, and Play Mode transitions. No sampled pose should
be persisted into scenes, prefabs, clips, or definitions.

For low-level inspection, add **Blend Shape Debugger**. Assign its Expression
Module and the module's reciprocal debugger reference. Author a Pose and use
Preview Pose / Stop / Restore. Starting either facial preview stops/restores
the other before taking ownership. The combined Batch 3 review prefab shows a
configured example. Shape authoring, database, previews, and debugger remain
supported reusable tools.

## Voice demo and acceptance

CharacterTesting and both backup scenes keep the existing voice buttons and
audio routes. VoiceLipSyncDemo explicitly selects the appropriate Speaking
happy activity when a voice starts. At audio end it explicitly returns to Idle
only if that same activity is still current. Other caller-selected activities
are left in place.

Durable Edit Mode fixtures under `CharacterSystem/Tests/Editor` cover definition
validation/serialization, dispatch and interruption, loops and one-shots,
expression blending, facial eligibility/ownership, preview restoration, and the
four production prefabs. Run them in Unity Test Runner's EditMode tab. Preserve
unsaved scene edits before using Test Runner's save/reload workflow. Session
verification executes the fixtures directly with setup/teardown, NUnit
assertions, and expected/unexpected log validation to keep the working scene.

Start Play Mode yourself for final visual/audio acceptance: voice switching,
blinking and random-motion eligibility, shared assets across independent
characters, one-shot return to Idle, runtime preview suspension/resumption,
new-command cancellation, disable/re-enable, pause/resume, and uninterrupted
visemes. A successful Web build verifies compilation/packaging; browser and
runtime visual behavior still require manual acceptance.

Batch-specific verification is recorded in `CharacterSystemBatch4.md` and
`CharacterSystemBatch5.md`.
