# Facial-hair rendering review — 12 October 2026

The material-only plan was tested in the live Unity 6000.5.9f1 Editor. **No candidate passed the visual-improvement requirement, so the original facial-hair material and assignments were retained.** The result still does not match the supplied Blender Cycles reference. No production material, mesh, prefab, scene, lighting, scalp-hair shader, startup reset, or lip-sync script was changed.

## Findings

- The active scene is `Assets/_Sandbox/Scenes/CharacterTesting.unity`. Its `Face` SkinnedMeshRenderer uses `Ethan_Hair` in material slot 6 (the seventh slot).
- The facial-hair submesh has 30,010 triangles and 40,388 referenced vertices. All referenced UVs are `(0, 0)`. Texture filtering cannot recover strand coverage from these UVs.
- The actual mesh has **73 blendshapes**, not the 74 stated in the attached plan. It has 74 skinning bones. No blendshapes or skinning data were edited.
- Skinned motion vectors are already enabled. Direct GPU readback found nonzero motion in the brow region during eyebrow raises and in the moustache region during head/jaw movement with the original settings. Enabling the material motion pass produced no useful visual improvement. These regional checks do not establish the correctness of every individual strand pixel.
- The installed URP motion-vector pass has no automatic fallback-material assignment. [Unity's documentation](https://docs.unity.cn/Manual/urp/features/motion-vectors-custom-shader.html) also describes removal of the older automatic fallback behavior. A disabled material pass alone was therefore not used as proof that the live output was missing vectors.
- The fullscreen motion-vector debug view returned a constant yellow image and was rejected as evidence. `RawMV_*.png` images come from direct readback of the actual R16G16 motion-vector render texture instead. Grey indicates zero motion; red/green offsets show signed motion magnified 100 times. `MotionSummary.json` records regional measurements.

## Candidates and outcome

All candidates were temporary material copies assigned only to the facial-hair slot. Brown base color, alpha clipping, geometry, TAA High, and native render scale were retained.

| Candidate | Specular RGB | Smoothness | Direct highlights | Explicit motion pass | Outcome |
| --- | --- | --- | --- | --- | --- |
| Original | 0.20 | 0.10 | On | Original setting (off) | Retained |
| MotionOnly | 0.20 | 0.10 | On | On | No useful visible improvement |
| Soft010 | 0.10 | 0.10 | On | On | Grain remains; darker hair |
| Soft004 | 0.04 | 0.05 | On | On | Grain remains; stronger edge contrast |
| Matte | Original 0.20 | Original 0.10 | Off | On | Grain remains; flatter and darker |

The close-ups show thin polygon edges and the existing uneven raised-hair silhouette. Changing specular response does not change their screen-space coverage. Lower specular values made the patches darker rather than visibly smoother. At the original framing, measured frame-to-frame moustache luminance variation was approximately 1.97/255 for Original and 2.12/255 for Soft004 at 1080p; at 720p it was 2.35/255 and 2.52/255. These measurements cover a rectangular patch including skin and are supporting observations, not a pure strand-only shimmer metric. Brow measurements also include moving scalp strands.

## Validation and captures

- Actual composited Game-view render targets at 1920×1080 and 1280×720; no off-screen `Camera.Render()` replacement or upscaled output.
- Each case ran for 180 observed camera-rendered frames, with 60 warm-up frames. Captures were taken at frames 140, 160, and 180.
- All five candidates were screened at the original framing (22° camera FOV) and in a closer framing (12° FOV). The camera FOV was restored afterward.
- Original and Soft004 were compared at both resolutions with a neutral face, ±8° head movement, animated eyebrow raises, smiles, jaw opening, and the existing `VoiceLipSyncDemo.PlayBoy()` playback. Audio was playing and viseme weights were nonzero in the lip-sync samples. Lip-sync output depends on wall-clock audio and is not an exact frame-for-frame pose match.
- No newly introduced detachment or lip-crossing problem was apparent in the sampled deformation frames. The existing jagged outline remains. Sparse captures cannot guarantee the absence of a brief artifact between samples.
- Approximately 80 GPU samples per case: the largest Soft004 median increase was **1.51%**, below the plan's 10% limit. Original/Soft004 stationary medians were 5.49/5.58 ms at 1080p and 5.04/5.11 ms at 720p. These are whole-scene Editor Play-mode timings, not standalone player benchmarks. See `GpuSummary.json` and `Validation.json`.
- The URP Lit shader had no shader messages. No new console errors were recorded after the baseline cursor; compilation succeeded. Historical scalp-hair shader messages were present before this review.
- The original scene was already dirty when inspected. It was neither saved nor reopened; the pre-existing unsaved scene state was preserved. Reopen verification is unnecessary because no candidate was persisted.

Open [ScreeningComparison.png](ScreeningComparison.png) for the original-framing crop comparison, [CloseupComparison.png](CloseupComparison.png) for the close-up comparison, and [PoseComparison.png](PoseComparison.png) for deformation and lip-sync samples. These sheets are presentation crops; the full frames retain their native capture resolution. Full frames use names such as `Original_Neutral_1920_140.png`, `Soft004_Head_1280_160.png`, and `Closeup_Matte_Neutral_1920_140.png`. `MV_*.png` are Game-view frames accompanying the second diagnostic run; use `RawMV_*.png` for vector visualization.

## Retained settings and rollback

`Ethan_Hair`: base RGB `(0.10, 0.05, 0.03)`, specular RGB `(0.20, 0.20, 0.20)`, smoothness `0.10`, highlights enabled. The original material-pass setting, skinned motion vectors, mesh, other material slots, High TAA, and render scale 1 remain unchanged. `FinalSettings.json` verifies the final Edit-mode state.

There is no persisted rendering change to roll back. For a manual comparison still running, call `EthanFacialHairComparison.Stop()` or exit Play Mode. The comparison utility is Editor-only and is excluded from player builds. It does not create or assign an `Ethan_FacialHair.mat` asset.

To repeat, enter Play Mode and evaluate the appropriate `Tools/Hair/RunFacialHair*.cs` file through `unity command eval_file --caller plugin --skill unity-cli --file <file>`. Wait for `Docs/FacialHair/Status.txt` to say `Completed` before starting another run. Output files are replaced by the new run.

## Next rendering pass

The visual evidence points toward screen-space coverage and the existing polygon silhouette as the main remaining problem. A follow-up should compare a representative brow/moustache geometry patch with better strand widths, orientation, and usable coverage UVs, while retaining skinning and testing its blendshape deformation. Keep the current mesh available for rollback. This work would extend beyond the attached plan's rendering-only limit; it was not performed here.
