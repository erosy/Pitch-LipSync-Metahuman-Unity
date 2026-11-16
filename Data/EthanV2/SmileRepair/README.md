# Ethan expression record ordering repair

This follow-up supersedes the previous claim that blink-only static previews
established full-character correctness. The supplied 13:28:46 recording shows
facial breakup during the runtime greeting/smile. That was not adequately
verified by the previous pass.

The previous sparse-record writer appended shading-only entries to the end of
each expression frame. **34 of 73 frames had indices out of ascending order.**
The installed asset now orders every frame by vertex index and has been
reimported to rebuild Unity's mesh buffers. The generator was also corrected
so future reconstruction keeps canonical ordering.

`canonicalization.json` proves every complete sparse record was preserved.
`installation.json` records an independent Unity API comparison: all 73 dense
position, normal and tangent arrays, frame names/weights and base mesh attributes
are bit-identical before/after. The change is record ordering and buffer refresh;
it does not alter expressions, topology, materials, lighting or hair settings.

The actual scene game-camera smile preview is `after_gpu_joy.png` (face crop:
`after_gpu_joy_crop.png`). Static evaluated full-character comparisons are under
`Live`. Additional direct camera captures can reuse a cached skinned pose in
Edit Mode; they are not proof of every requested speech/blink combination and
must not be reported as runtime verification.

Existing Edit Mode character checks passed again: 657 secondary face-sync checks,
lip-sync callback/eight phonemes, avatar, neutral skinning, Waving and idle/Thinking
sampling. Temporary preview was stopped and all face weights restored to zero.
No Play Mode transition or scene save was performed. Full runtime replay of the
user's greeting remains unverified under the project's user-controlled Play Mode
rule.

Head remains 104,632 vertices / 187,041 triangles; brows/lashes remain 84,405
vertices / 148,734 triangles. Sparse shape payload remains approximately 88.24 MiB
and Unity's final mesh memory estimate after reimport is 117.05 MiB. Scalp stays 32
points per strand / 30 Hz.

Tools: `canonicalize_shape_records.py`, `InstallCanonicalFace.cs`, and the updated
`preserve_normal_shape_records.py`. `recovery.zip` contains the pre-ordering head
asset for diagnosis; it contains the defective ordering and is not a recommended
production rollback. The older pre-normal-repair recovery remains in
`../NormalRepair/recovery.zip`.
