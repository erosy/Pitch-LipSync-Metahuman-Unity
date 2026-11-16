# Jiwoo selection and Liora speech

`CharacterTesting` starts with Ethan active and Jiwoo inactive. Both Boy and Girl
are visible. Girl is positioned below Boy with a 12-unit gap to avoid their
previous overlap. Their only persistent callbacks are `SelectBoy` and
`SelectGirl` on the always-active scene-level `CharacterSelectionController`.

Selecting the other character disables the previous root, which cancels its
flow, speech and warmup through existing lifecycle methods. Activating the
selected root starts its normal warmup/readiness and greeting. Selecting the
already-active character calls its existing `PlayResponse`, retaining readiness
checks and interruption behavior. Loading-time response requests are ignored.
The legacy demo's direct audio playback methods are not wired to either button.

Jiwoo retains Ethan's shared waving greeting, `Remy@Idle`, `Remy@Thinking`, and
the existing talking animation, expressions, transitions, gaze and hair settings.
Her dedicated response flow reuses the shared thinking and speech states but
has its own one-second default-idle pause. The existing shared pause is 0.1
seconds; it was preserved so Ethan's behavior did not change.

Both Jiwoo's prefab and scene instance use `liora.mp3` and `Profile_Liora_ID`.
The eight phoneme mappings are resolved by shape name rather than copied mesh
indices: A → `viseme_aa`, I → `viseme_I`, U → `viseme_U`, E → `viseme_E`,
O → `viseme_O`, S → `viseme_SS`, M → `viseme_PP`, N → `viseme_nn`.
Existing gains (A 0.34, I 0.5, others 1), volume thresholds, smoothing, and
phoneme blending are preserved. The character's own AudioSource feeds uLipSync,
whose sole persistent listener drives Jiwoo's Face. Silence uses the existing
driver's neutral behavior.

## Verification

Compilation and static saved-asset checks passed. Checks cover initial selection,
button visibility, overlap and callbacks, Humanoid validity, animation/flow
references, one-second pause, profile phonemes, viseme names, and voice routing
in both the prefab and scene. All 29 fingerprinted Ethan/shared assets remain
unchanged. Results are in `verification.json` and `compilation.json`.

No Play Mode changes, audio or animation previews, live tests, or builds were
performed. Runtime validation remains with the user:

1. Start Play Mode: Ethan should greet normally.
2. Press Girl: Ethan hides; Jiwoo becomes ready and waves.
3. Press Girl again: thinking, one-second idle pause, Liora speech, then idle.
4. Switch to Boy during speech: Jiwoo stops and Ethan greets on activation.
5. Check repeated selections, loading-time clicks, lip-sync appearance, and
   neutral mouth after speech. Existing facial/hair limitations still apply.

`Tools/Hair/ConfigureJiwooSelection.cs` provides reproducible `Apply`, `Inspect`,
and `Verify` entry points for the Unity CLI in-memory script runner. Apply
requires Edit Mode and a saved, clean CharacterTesting scene. Verification
inspects configuration without invoking selection, response, audio, or animation.
