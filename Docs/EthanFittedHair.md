# Ethan fitted hair

## Applied setup

`Ethan.prefab` and `CharacterTesting.unity` use the new
`Groom Hair Asset/Fitted/Ethan_BlenderFitted.asset`.

- Source: `C:/Users/ejpul/Downloads/testing.blend`, object
  `01_Center part curtains.001`. The original blend file was read without saving it.
- Export evaluates its Bezier handles, resamples each strand to 32 points by arc
  length, and retains all 94,408 curves. This is a Custom Hair Asset with a
  `BlenderFittedHairData` provider; the original Alembic Hair Asset remains available.
- Geometry is expressed directly in Unity Head-bone coordinates. Hair's local
  position and rotation are zero, and scale is one.
- A rigid skull registration has a median nearest-surface error of 0.311 mm.
  4,233 roots behind the nearest head surface were lifted to 0.8 mm clearance;
  the correction fades over the first eight particles.
- `Head Hair SDF` is a standalone Hair Boundary under Head. Its 96-cubed RFloat
  texture covers a 0.30 x 0.355 x 0.33 m region, including the forehead and ears.
  Previous head/ear sphere objects remain disabled. Only the resident SDF is used.
- Physics and rendering use Manual LOD 1. The fitted asset has one full-detail LOD
  to eliminate follower interpolation during this baseline test.
- Physics: Gauss-Seidel, 4 substeps, 12 iterations, stiffness 1; Stitched Local
  Shape 1, bias 0.7; Global Position 0.3 per 100 ms; Global Rotation 0.7;
  gravity 0.2; linear damping 0.2 per 100 ms; solid margin 0.35 cm.

## Validation and limits

Tested in the live Unity 6000.5.9f1 Editor during CharacterTesting animation.
The SDF boundary was valid, sampled particle positions were finite, and 836
sampled roots tracked their transformed targets within 0.000124 mm. Of 30,172
tested non-root positions inside the SDF region, four were deeper than 2 mm;
the worst sample was about 5.30 mm. This is a sampled diagnostic, not proof of
zero intersections throughout every animation. Rendered segments between
particles can also cross geometry.

The SDF uses nearest oriented surface distance around the scalp/ears. It is not
a watertight interior classification for open mouth/eyes, and is baked in the
neutral mesh pose. It follows Head rigidly but does not rebake for facial blend
shapes. The Blender file's custom DNA drivers were disabled during background
inspection/export; the saved mesh and curve data were used.

Full-detail simulation of 94,408 x 32 particles with 4 substeps and 12 iterations
is expensive. A DirectX 12 upload-buffer resize warning occurred in Play Mode;
no new C# compilation errors or sampled non-finite particles were observed.
This baseline needs a separate performance/LOD pass before shipping, particularly
for a web target. The current assets also retain the export data for rebuilding,
which increases build dependency size.

Shader lighting and antialiasing differ from Blender's offline render. The saved
preview documents this implementation; it does not claim a pixel-identical render.

## Comparison and rebuilding

`Ethan_BeforeFittedHair.prefab` preserves the preceding prefab setup. The original
`SC_Hairstyle_Male_11.asset` is untouched. The scene snapshot shares prefab
references, so use the independent backup prefab for an original-groom comparison.

Generation sources are in `Tools/Hair/`: first run `ExportEthanHeadForFit.cs`
through Unity CLI `eval_file` in the neutral pose, then run Blender in background
with `testing.blend` and `build_fitted_groom.py`. This produces the fitted curve
binary and SDF binary. The custom provider consumes the curve binary when the
Hair Asset is rebuilt. Reimporting/rebuilding does not automatically refresh the
Texture3D asset from the SDF binary; recreate its pixel data through the Editor
API after changing a bake. Do not change the head geometry or rig scale without
rebaking the collision field and checking registration.

See `FittedGroomBuild.json`, `FittedHairValidation.json`, and
`EthanFittedHairPreview.png` for the saved audit results.

## Hair rendering pass — 12 October 2026

Selected **Hair Smoothness 0.2** on `Ethan_StrandHair.mat` to reduce the fine,
bright specular grain. The existing Shader Graph now exposes **Hair Width Scale**
and **Hair Smoothness** in its material inspector and blackboard. Width stays at
1.0. High TAA, Render Scale 1, MSAA disabled (sample count 1), and render
subdivision disabled remain the saved configuration. The fitted groom, 94,408
strands, 32 simulation points, physics, attachment, and collision settings remain
unchanged. The Blender source and reference image were not modified.

The starting camera already used High TAA. Very High TAA softened detail with
less benefit than reducing specular smoothness. SMAA High plus 4x MSAA produced
much brighter grain in this fine-strand groom. Render subdivision produced 63
render vertices per strand without rebuilding or resampling the 32-point
simulation, but offered little visible improvement and increased measured GPU
time by approximately 72%. Width 1.25 had little visible benefit. Render Scale
1.25 was not needed after the native-resolution material improvement.

Verified 1920x1080 Editor GPU medians (120 distinct timing samples per pose):

| Configuration | Stationary GPU | Animated GPU |
| --- | ---: | ---: |
| Baseline: High TAA, smoothness 0.5 | 5.432 ms | 5.466 ms |
| Selected: High TAA, smoothness 0.2 | 5.507 ms | 5.548 ms |
| SMAA High + 4x MSAA | 4.456 ms | 4.499 ms |
| Render subdivision 1 | 9.366 ms | 9.416 ms |

The selected setting increased these medians by 1.4–1.5%, below the 10% target.
At 1280x720, baseline and selected animated medians were both about 5.001 ms;
the grain reduction remained visible. These are `FrameTimingManager` samples
from the live Editor on an RTX 5060 Laptop GPU / Direct3D12, not an isolated
Player benchmark. Editor scheduling, additional views, GPU clocks, and idle
presents affect them. Compare medians within a run; do not compare the earlier
exploratory run's absolute times with the later verified run.

Captures read the actual composited Game-view render target without calling
`Camera.Render()`. Every pose warmed for at least 60 rendered frames. The
comparison replayed `Breathing Idle` from normalized time 0.25 with a 1/60-second
capture timestep. Motion recordings sample eleven Game frames six rendered
frames apart, covering one second of the same animation segment. Front and
60-degree side views use matched camera positions, lighting, and resolution.
An initial capture used the current Editor GUI's size for several frames;
`*Verified*` captures and timings supersede affected exploratory results.

The generated URP MotionVectors pass uses Hair Vertex's object-space velocity;
its previous-position calculation subtracts that velocity. Built-in debug
captures showed head-animation motion and residual strand deformation with the
head held still after a temporary 8-degree nudge. The shader connection needed
no repair. The probe restored the Head transform and Animator; no physics
parameters were changed. Stationary settled motion returned to zero.

No new persistent hair trails, thicker silhouette, or shadow regression was
apparent in the sampled comparisons. Reduced highlight contrast also makes the
hair darker. Fine silhouette flyaways and sparse fringe geometry remain; this
rendering change does not remove stray strands or reproduce Blender's offline
lighting. The existing duplicate `PROCEDURAL_INSTANCING_ON` shader diagnostic
was present before this pass and remains. Script compilation passed.

The material survived saving and reopening `CharacterTesting.unity`. A final
sampled geometry check found a valid SDF, no non-finite sampled positions, and
maximum root tracking error about 0.000123 mm across 836 roots. Of 30,172
sampled non-root positions in the SDF region, none were deeper than 2 mm in
that animation frame. This retains the sampling limits described above.

Artifacts are in `HairRendering/`: `BeforeAfter.png`, `BeforeAfterMotion.gif`,
full-resolution `*Verified*` captures, `*720*` captures, motion-vector debug
images, `Comparisons.json`, `SavedSelection.json`, and `FinalGeometryCheck.json`.
`PixelStatistics.json` gives descriptive values for one fixed inner-hair ROI;
its adjacent-pixel contrast fell from about 6.51 to 2.10. That includes the
darker material response and is not a general measure of rendering quality.

For simple visual rollback, set Hair Smoothness to 0.5 and Hair Width Scale to
1.0. Original working shader/material/prefab/scene/pipeline file contents are
also preserved as `.txt` files under `HairRendering/Rollback/`. These include
pre-existing project work; they are not copies of Git HEAD.

`Tools/Hair/ExposeRenderingControls.cs` adds the controls using Shader Graph
APIs through CLI `eval_file`. `EthanHairRenderingComparison` is an Editor-only
comparison runner; after entering Play Mode, run its `Start()` through CLI eval.
It restores temporary material, camera, AA, render scale, subdivision, and
capture-timestep settings when it finishes or Play Mode stops. Rerun
`Tools/Hair/review_rendering.py` to regenerate the comparison sheets and GIF.

## Ear tips and startup reset — 12 October 2026

The live ear diagnosis found rest-field penetrations and collision-margin
contacts. A separate `Ethan_EarClearance` groom was built for clearance/temple-tip
experiments; clearance alone did not remove the visible tips and one sampled
intersection worsened. This variant was **not selected for Ethan**. The original
`Ethan_BlenderFitted.asset` remains assigned. The experimental exporter and
captures under `HairRendering/EarCorrection/` are retained for investigation.

Following the requested loading workaround, `EthanHairStartupWarmup` is attached
to Ethan's prefab and scene instance. It keeps Ethan active, hides body renderers
and disables procedural hair rendering, lets the Animator establish its pose
for 8 frames, calls `ResetSimulationState()` once, waits 3 more frames, and
restores visibility. Animator culling is temporarily Always Animate so hiding
renderers does not prevent pose initialization. Renderer states, hair rendering
settings, and Animator culling are restored, including if the component is
disabled mid-warmup. Physics, collision, groom geometry, and the user's current
material width/smoothness values are unchanged.

This is a startup-only workaround. It can hide initialization artifacts; it does
not guarantee that collision-driven protrusions will not recur during gameplay.
Change Pose Warmup Frames and Frames After Reset on the Ethan root's component
to adjust the hidden interval. Disable/remove the component to revert the startup
behavior. The visibility/reset trace and Game-view captures are saved under
`HairRendering/StartupWarmup/`.

Validation: script compilation passed. The Game-view observer recorded zero
visible body renderers and Disabled hair rendering in the initial loading
frames. Reset count advanced once; body visibility and BuiltinStrips hair
rendering returned by observed frame 10. At observed frame 1200 (about 16.7
seconds of simulated time after reset), readiness remained true and reset count
was still one. `Frame4.png` shows the hidden interval; `Frame24.png` and
`Frame1200.png` show the revealed character. The temporary automatic observer
was removed after validation. Material Width Scale 1.368 and Smoothness 0.275
were preserved from the user's current values.
