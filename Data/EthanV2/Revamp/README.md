# Ethan V2 native-armature candidate

Candidate: `Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab`.
This is a separate review prefab. Production selection and the existing saved
scene have not been switched. `production_preservation.json` checks their hashes.

## Result

The candidate has `Armature`, `Face`, `Outfit`, and `Modules` at the root. Scalp
hair stays beneath the native head, under its `Hair` attachment, so it follows
the skeleton without another driver. There is one Animator with a calibrated,
metre-based native Humanoid Avatar. The old-Ethan animation skeleton, pose bridge,
Rigify control hierarchy, and facial synchronization component are absent.

`Face` combines the corrected production head, shirt-coverage body, eyes, teeth,
inner mouth, cartilage and saliva, retaining 13 material slots and all 73 ordered
expressions. The original corrected head remains first, in unchanged coordinates
and vertex order. Its position/normal/tangent expression arrays match exactly,
including tiny sparse position records and canonical ordering. No fresh dense
FBX head was substituted. `Outfit` retains the current collar-restored mesh.

Native body and facial deform bones are consolidated into one armature. Weighted
facial/helper bones are retained; this is not a speculative bone-reduction pass.
The garment's 21 remaining Rigify influences use static bind-offset helpers under
native bones, with no runtime pose copying. Jaw/ear-labelled collar helpers use
the head rather than the old proximity-based shoulder guess. Their neutral
placement and vertex weights are unchanged.

## Preservation and cost

| Measure | Source | Candidate |
| --- | ---: | ---: |
| Skinned renderers | 12 | 2 |
| Character mesh vertices | 159,712 | 159,712 |
| Character mesh triangles | 274,793 | 274,793 |
| Brow/lash vertices / triangles | 84,405 / 148,734 | unchanged |
| Active mesh memory estimate | 281.03 MiB | 192.45 MiB |

Candidate Face has 149,611 vertices / 257,691 triangles; Outfit has 10,101 vertices
/ 17,102 triangles. Face mesh memory is approximately 162.61 MiB. Its sparse
blendshape vertex payload is about 123.23 MiB (3,230,421 records × 40 bytes),
excluding frame/channel metadata. These are mesh estimates, not measured FPS or
total application RAM/VRAM.

The original body had zero position/normal deltas, no controller references, and
no animation blendshape curves. Its unused tangent payload was excluded from
the combined Face: otherwise facial expressions would newly animate body shading.
`body_channel_audit.json` records that proof. Removing these inactive records
reduced the initial merged payload from roughly 212.83 MiB to 123.23 MiB.

The scalp retains the same asset, 94,408 strands, 32 points, fixed strand scale,
30 Hz, disabled Edit Mode simulation, collision texture and readiness wiring.
Maximum checked root/tip placement difference is 0.000387 mm.

## Validation and review limits

- `static_verification.json`: exact corrected-head data; zero converted expression
  position error; maximum neutral skin difference 0.000720 mm and outfit difference
  0.000371 mm, within the 0.001 mm target.
- `motion_verification.json`: seven Humanoid clips / 63 sampled poses, valid
  native Avatar, 30 finger mapping checks, eight phonemes and matching lip-sync
  callback/module references. Numeric pose checks establish bounded movement;
  visual comparisons remain necessary for fit and animation quality.
- `hair_verification.json`: endpoint, settings, native-head attachment, collision
  reference and startup Animator checks. No simulation was run.
- Eighteen paired images are in `Previews`: neutral, left/right blink, raised brows,
  smile, speech, Waving, Thinking and Thumbs Up. These are evaluated static meshes
  in identical preview lighting. Scalp simulation is omitted from these images;
  it remains present in the prefab. Facial image mean channel differences are
  below 0.003/255. See `preview_metrics.json`.
- Final structure has no missing scripts or character configuration errors.
  Unity reports no compilation failure. No public runtime APIs were changed.

Native retargeting is not pixel-identical to the old driver's motion. Review arm
reach, hands, shoulder/collar contact and garment clipping in the supplied motion
comparisons before accepting it as the production replacement. Some collar/shoulder
penetration is still visible in demanding sampled poses; do not represent the
bounded-pose tests as full clothing-fit approval.

No Play Mode transition, audio playback, build, scene save or production switch
was performed. Full greeting/response playback, gaze, speech combinations, hair
settling and final clothing contact still require user-controlled Play Mode review.

## Reproducible workflow

Editor tools live under `Tools/EthanV2`. They use Unity CLI `run_script` and refuse
to overwrite an existing candidate prefab; preserve later candidate edits before
attempting a rebuild. Do not run them against the production prefab as a target.

1. `RevampEthanV2Candidate.ProbeAvatar` validates native mappings on a disposable
   rig and saves the preliminary candidate Avatar.
2. `RevampEthanV2Candidate.Build` unpacks only an isolated copy, creates the merged
   Face/Outfit and candidate prefab, and rewires user fields. Unity's internal
   component identity fields are never reference-remapped.
3. `VerifyRevampedEthan.AuditBodyChannels` verifies that body channels are inactive.
4. `RevampEthanV2Candidate.RebuildMeterRig` restores native authored bone placement,
   normalizes units and builds an Avatar on a clone with no existing Animator.
5. `RevampEthanV2Candidate.CalibrateAvatar` calibrates arm chains to a horizontal
   reference T-pose while preserving the prefab's authored rest pose.
6. `RevampEthanV2Candidate.RefineClothingHelpers` fixes anatomical collar parenting.
7. Run `preserve_candidate_head.py`, then `VerifyRevampedEthan.ImportPreservedHead`
   to restore the original head sparse records and omit inactive body tangents.
   Always stage from the current candidate mesh so final bind poses are retained.
8. Run `StaticChecks`, `MotionChecks`, `HairChecks`, and `RenderRevampedEthan.Run`.
   Inspect the images, then use `VerifyRevampedEthan.Finish` for the final report
   and cleanup of unused candidate-only Avatar experiments.

An initial conversion attempt crashed Unity during cleanup after a failed save.
The conversion's internal-reference handling was corrected, the Editor was
recovered by the user, and the subsequent candidate saved successfully. Historical
failed-attempt reports are diagnostic records, not delivery verification.
