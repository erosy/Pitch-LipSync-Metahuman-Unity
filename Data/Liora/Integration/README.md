# Liora consolidated character hierarchy

Updated 18 November 2026. This supersedes the earlier separate-section candidate. The expression shading has also been repaired; see [shading repair and matched previews](../ShadingRepair/README.md).

The current prefab is [Liora_Candidate](../../../Assets/_Sandbox/Characters/Liora/Candidate/Liora_Candidate.prefab).
It now uses the same root layout as the current Ethan V2 candidate:

```
Liora_Candidate
  Armature                         one native Animator and Humanoid Avatar
    ... head
      Hair / HeadCollision / ScalpHair
      Voice
      SpeechGazeFrame
  Outfit                           SkinnedMeshRenderer
  Modules
    Animation                      AnimationModule
    Emotion                        ExpressionModule
  Face                             SkinnedMeshRenderer + uLipSyncBlendShape
```

There are exactly **two skinned renderers**, Face and Outfit, and no BAKED_ objects
in the candidate hierarchy. Head, body, separate mouth/eye/teeth sections and the
adaptive brows/lashes are material sections of the merged Face. The redundant,
already-disabled eyelash cards are omitted from the active candidate; the original
mesh assets and source FBX remain preserved.

The face/lip-sync references and persistent audio callback now point to Face.
One name-based facial synchronization binding remains only for Outfit, because
its original small nonzero expression records have been preserved. Hair,
collision, native skeleton, Avatar, voice profile/clip, gaze frame, activity assets,
warmup/readiness and flow settings remain unchanged.

Current validation, section manifest, rollback and previews are documented in
[the hierarchy correction report](../HierarchyRepair/README.md).

- Face: 100,369 vertices / 175,832 triangles / 73 expressions.
- Outfit: 5,196 vertices / 9,029 triangles; original expression records retained.
- Scalp: 100,477 strands, 32 points/strand, 30 Hz, Fixed strand scale, no Edit Mode
  simulation, fitted head collision.
- Brows: 24,063 vertices / 38,568 triangles; lashes: 11,190 / 17,484. Both retain
  adaptive 3-8-ring geometry and all expressions inside Face; maximum source curve
  approximation error remains 0.075 mm. Dense position shape allocation for both
  sections is 29.45 MiB, or 58.90 MiB including dense normal deltas. Unity reports
  258.12 MiB for the entire merged Face, not a separate mesh allocation per section.

The first separate-section integration report is preserved as
[README-separated-stage.md](README-separated-stage.md) for source provenance,
production widths/material decisions and the original measurements. Its hierarchy
and renderer list are historical; delivery.json now describes the corrected prefab.

The initially dirty scene was not saved or replaced. No Play Mode transition or
build was performed. Expression shading and animated garment fit have since been repaired. See [clothing fit and before/after review](../ClothingFit/README.md) and [shading repair](../ShadingRepair/README.md). User-controlled runtime review remains necessary.


