# Ethan V2 confident idle

Created as separate review assets. No character, prefab, scene, or flow was assigned
the new activity; the existing idle and expression are preserved.

## Assets

- `Assets/_Sandbox/Animations/Confident Idle.anim`
- `Assets/_Sandbox/Data/Animation/Confident Idle.asset`
- `Assets/_Sandbox/Data/Expression/Confident Idle Upward.asset`
- `Assets/_Sandbox/Data/Activity/Confident Idle.asset`

The new activity links the copied animation and expression, with `Is Idle` enabled.
The animation retains the source definition's speed 1 and crossfade 1 second.
Assign the new activity manually only after reviewing it.

## Pose

Source: `Assets/_Sandbox/Animations/Remy@Idle.fbx` / `Idle`, 8.333334 seconds,
30 fps. The original clip is copied into a standalone humanoid `.anim`.
Six head/neck muscle curves receive a calibrated offset: chin up 5 degrees,
turn toward screen-left 3 degrees, and lean toward screen-left 2 degrees.
Screen-left means Ethan's right when viewed from the front. Roughly one-third
of the rotation is applied through the mapped neck and two-thirds through the head.
The original idle motion continues around this offset.

The expression copies `Confident_idle` without retuning its existing facial weights.
`eyeLookUpLeft/Right` are 10, and `eyeLookDownLeft/Right` are 0 (Unity's 0–100 scale).
Gaze remains in the expression, outside the body clip. Existing facial motions can
temporarily override these shapes and return to this base. Speech gaze can still
look toward the audience while speaking.

## Verification and previews

`verification.json` records ten sampled poses, exact preservation of 124 other
curves, the matching head loop seam, definition validation, and target-angle checks.
The sampled head orientations match the requested offset within Unity's quaternion
angle precision. Body rotation differences are zero; maximum sampled body position
difference is approximately 0.206 mm from humanoid retargeting.

`Previews/Comparison.jpg` shows front and three-quarter comparisons and several
phases of the edited loop. Twelve full PNGs are retained beside it. These are
CPU-skinned static mesh renders: scalp strands are omitted and hair simulation
was not run. The top row shows the original at left and the copied pose at right.

No Play Mode transition or build was run. Authoring and verification use disposable
prefab instances and preview scenes; live scene transforms are not sampled or saved.

## Reproduce

Use the installed Unity Pipeline `run_script` command with
`Tools/EthanV2/CreateConfidentIdle.cs` and entry points
`CreateConfidentIdle.Inspect`, `.Build`, `.Verify`, or `.Render` in Edit Mode.
`Build` refuses to overwrite any of the four output assets. The script is outside
`Assets` and adds no runtime components or assembly dependencies.
