# INA modular facial system

`ExpressionController` remains the character component and retains its component
identity, Face, and expression commands. Definitions are ordinary Unity serialized
data. Odin presents the groups, lists, sliders, shape selectors, and runtime status.
uLipSync still owns its configured visemes; the Animator/body setup is unchanged.

## Migrate Character Sample manually

1. Stay in Edit Mode, select **Character Sample** in **CharacterTesting**, and expand
   **ExpressionController**. Stop / Restore any active debugger preview first.
2. Click **Migrate Facial Setup**. It checks all old Face references before editing,
   copies the live expression, blink, and idle settings, then removes that character's
   BlinkController and IdleFaceController in the same Undo operation.
3. Review **References**, **Expressions**, and **Activities**. Face, lip-sync
   references, intensity, crossfade, and speech-end delay remain on the same component.
   The debugger is optional; leaving it empty is fine.
4. Test **Undo**: the old components and definitions should return together.
   Migrate again to proceed, then review and save the scene yourself.

The migration button becomes disabled after conversion and cannot duplicate or
overwrite migrated definitions. Legacy scripts and serialized expression fields
remain available for characters you have not migrated. Their modular scheduler
stays inactive. Migration does not save the scene.

Migration copies **current Inspector values**, including unsaved tuning. At the
implementation's read-only check, Character Sample's purse hold was **0.5–1.5 s**,
twitch hold **0.5 s**, expression fade **0.18 s**, and speech-end delay **0.15 s**.
Those differ from some fresh preset values and are deliberately retained.

## New character setup

Add ExpressionController, assign Face and the existing uLipSync references, and
inspect the default definitions. **Load INA Defaults** explicitly replaces only
the expression/activity definitions after confirmation; it retains references and
global expression tuning. It refuses to replace a setup that still contains old
blink/idle components; migrate that setup first.

To add an optional BlendShapeDebugger, assign its **Character** reference and assign
the debugger in ExpressionController's **References**. The debugger obtains Face
from the character. Fill its shared **Pose** and use **Preview Pose**, then
**Stop / Restore**. Preview is an explicit action in both Edit Mode and Play Mode.
During a preview, modular output yields while expression/speech state still updates.
On release, the current expression is reapplied and activities receive fresh waits.

## Author another activity without runner changes

Add an item under **Activities** and give it a unique name and a channel string.
Activities in one channel take turns; different channels can overlap. After each
sequence, the channel chooses uniformly among currently eligible activities and
waits that chosen activity's sampled interval. Purse/twitch share `Mouth` and are
equally likely. `Gaze` and `Blink` run independently.

Set eligibility (`AllExpressions`, `NeutralOnly`, or `SelectedExpressions`) and
whether speech/transitioning are permitted. An empty selected expression name means
Neutral. The INA idle presets require silent, settled Neutral. Blink permits all
expressions, speech, and transitions.

Add named **Variants**. A variant is a complete pose selected together, such as the
two shapes for character-left gaze. Each target has its own base weight; one
**Strength** multiplier is sampled for the entire variant. For a 5–10 twitch, use
base weight 10 with strength 0.5–1. For paired gaze at 15–25, use base weights 25
with strength 0.6–1. The shape dropdown reads names from this character's mesh, and
the shape-name field also accepts custom names.

The sequence is **Waiting → Easing In → Holding → Easing Out**. In/out use smooth
interpolation. Hold is sampled once; elapsed time carries across phase boundaries.
After easing out, optional repeats insert **Repeat Gap**, with eyes/mouth/gaze at
zero activity weight, then reuse the same variant, strength, and phase timings.
Repetition probability is rolled once per sequence. A successful roll runs exactly
the configured number of additional repetitions (bounded to 0–32); repeats do not
roll again. For double blinks, use probability 0.15 and maximum additional repeats 1.

All activity timing uses scaled game time. Speech-end grace uses unscaled time,
preserving the prior behavior. Loss of eligibility immediately cancels the activity
and clears its contribution. Resuming starts a fresh interval.

Expressions are persistent: the selected silent/speaking pose remains active until
the command or speech state changes. They use the same target/pose data but do not
have finite hold/repeat sequences. `SetExpression(string)`, `Joy()`, and `Neutral()`
remain usable by existing buttons and scripts. Neutral is an empty expression name.
Expression intensity retains its prior scaling behavior, including expression
output above 100 at intensity greater than 1. Activity output remains clamped to 0–100.

## Validation and ownership

Warnings identify missing references/shapes, duplicate names/targets, invalid
numeric ranges, and ownership conflicts. Missing/reserved targets disable an entire
activity variant so paired eyes cannot become partially bound. No valid variant
disables the activity. Expression shapes and uLipSync visemes are reserved from
activities. Cross-channel conflicts disable all affected activities; shapes may be
shared within a channel. Numeric inputs are defensively bounded at runtime without
rewriting authored definitions. Optional debugger absence generates no warning.

Only cached, owned shapes are cleared on cancellation, disable, and renderer changes;
debugger ownership prevents resets during a held preview. Eyelids, mouth, gaze, and
expression weights remain separate for the supplied presets.

## Verification and your manual checks

The implementation compiled in Edit Mode and passed 18 data-only checks. These
cover phase carry-over/frame rates, synchronized strengths, bounded repeats,
channel selection/exclusion/overlap, eligibility cancellation, pause/re-enable,
missing/reserved variants, persistent expressions, numeric validation, conversion,
and Unity serialization. Run them again via **Tools → Facial System → Run Data-only
Checks** in Edit Mode. They create no meshes/renderers and sample no animation.

Character Sample was inspected without migrating it. Its scene file, INA FBX, and
legacy blink/idle scripts were left unchanged. No Play Mode or preview was invoked.

After migrating and saving, test playback yourself:

- Blink remains active while silent, speaking, Neutral, and Joy. At repeat probability
  1 with maximum additional repeats 1, each sequence contains exactly two blinks;
  restore 0.15 afterward.
- Silent, settled Neutral permits mouth/gaze overlap. Joy permits neither idle
  channel. Speech or expression transitions cancel current idle gestures immediately.
- Returning to silent Neutral waits for the transition and a fresh random interval.
- Paired gaze moves consistently; tune the initial weights visually if needed.
- Crossfades, speech-end delay, lip sync, and body animation retain their behavior.
- Debugger Stop / Restore resumes the current expression with fresh activity waits.
- Disable/re-enable and pause/resume leave no stuck weights.
- Add another Inspector activity and confirm it runs without changing runner code.

Migration Undo and visual behavior remain manual acceptance checks.
