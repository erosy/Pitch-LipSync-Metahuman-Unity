# Liora animated clothing fit

18 November 2026. The saved `Liora_Candidate.prefab` and active scene Outfit now
use `Assets/_Sandbox/Characters/Liora/Candidate/Liora_Outfit_Fitted.asset` with
its matching **95-bone array**. The Face/Outfit hierarchy is retained.

## Cause and repair

The garment was bound to Rigify-derived static helper bindings while the skin
followed the native Humanoid skeleton. Thinking and Talking twisted the garment
and skin differently, exposing chest, side/waist and collar skin. This was a
skinning/clearance problem, not renderer disappearance.

A first local upper-body transfer left waist breakthroughs visible in Thinking,
so the final pass rebinds the full garment to its corresponding native torso,
neck and shoulder surface. Correspondence uses closest triangles and barycentric
body weights with an anatomical filter excluding forearms/hands. This avoids
using an approaching Thinking hand as the underlying shirt surface. Coincident
seam positions share correspondence, and clearance corrections are reconciled
across seam groups.

Each garment vertex's neutral position is compensated through its actual weighted
skinning matrices. Original position/normal/tangent expression deltas are likewise
converted for the new skinning frame; their names, order and weights remain.
Unused bone slots were removed through an explicit index remap, reducing the
original 706 slots to 95 references to existing native transforms. No skeleton
bones, Avatar or animation clips were edited. The actual renderer policy is
Unlimited, and the fitted mesh uses at most eight normalized influences.

Bounded local clearance follows the weight repair. This is not a uniform garment
inflation: **1,305 of 5,196 vertices** move more than 0.01 mm in neutral world space.
Mean displacement over the whole shirt is **1.040 mm**; the maximum accumulated
movement relative to the original garment is **10.0002 mm** (10 mm cap plus float
rounding). All 5,196 vertex weight bindings were updated. The reported displacement
is the total across the fitting passes, not a per-pass limit.

The renderer bounds include all sampled fitted poses. The mesh and bone array
were assigned together to the prefab and matching live scene renderer.

## Preservation

- Outfit remains **5,196 vertices / 9,029 triangles / 73 expression frames**.
  Unity's mesh memory estimate is approximately **15.76 MiB**.
- Garment topology, all UV channels, colors, material slots, shape names/order and
  frame weights are preserved. Final imported position/normal/tangent arrays match
  the compensated source arrays exactly. Tiny sparse records are retained in
  ascending index order; temporary staging tangent sentinels are absent.
- Neutral compensation error beyond the prescribed clearance is at most
  **0.000358 mm**. Weight normalization error is below 0.00000012.
- Face's SHA-256 is exactly unchanged. Therefore all body/head geometry, the
  repaired facial shading, eyes, mouth sections and facial-hair expressions remain
  unchanged. No skin triangles were hidden, masked or deleted.
- Face remains 100,369 vertices / 175,832 triangles. Brows remain 24,063 vertices /
  38,568 triangles; lashes remain 11,190 / 17,484. Their dense position-only shape
  allocation remains 29.45 MiB combined, or 58.90 MiB including dense normal deltas.
- Avatar, clips, materials, voice/gaze, readiness, grooms and 32-point/30 Hz hair
  settings are preserved. No new runtime garment component was needed.

## Verification and limits

The fit uses **98 disposable Edit Mode pose samples** across neutral, Thinking,
Talking, Waving, idle and Thumbs Up, with neutral and speech/smile combinations.
An independent **48-pose validation** uses the actual Thinking, Speaking happy
and Greeting activity definitions, including authored Thinking targets, full
Greeting Joy and AA/FF speech poses. All samples have finite bounded skinning;
maximum coincident seam separation is **0.00853 mm**.

[Previews](Previews) and [ActualPreviews](ActualPreviews) contain matched
front/side/back before/after renders at multiple phases. They explicitly evaluate
both shape and skin deformation and use unchanged materials. Final images use the
preserved original mesh and original bone list for Before, even after installation.
Thinking, Talking, Waving and neutral front/side/back images were visually reviewed;
the large chest and waist skin patches seen in the original are absent in the
reviewed corrected poses. Collar/armhole openings remain part of the sleeveless
shirt design.

Each numerical surface check probes the garment vertices, triangle centers and
all edge midpoints (**41,312 probes per pose**), against retained head/neck/body
skin. For the independent actual-activity set, nearest-surface contact proxies
below -0.5 mm decrease from **214,485 to 31,958**, about an **85.1% reduction**.
The fitting set decreases from 446,036 to 65,614. These counts include internal
folds and regions near open collar/armhole boundaries: the nearest-normal measure
is not a signed distance to a closed clothing volume and does **not** certify zero
intersections. Remaining contact proxies are reported rather than described as
zero contact. Matched depth-tested renders provide the visible fit evidence.

This is numerical skinning plus static pose review, not observed runtime playback
of every animation transition. Please replay Thinking and speaking under your
control for final runtime acceptance. The user stopped Play Mode before asset
work; the agent did not enter or exit it. No WebGL build was run.

Final saved/live references, two-renderer hierarchy, Avatar, Character/flow
configuration, grooms and valid bone references pass. The installer call exceeded
the tool server's 30-second main-thread timeout while saving, but completed; the
installation report and subsequent independent check confirm the saved and live
assignments. Console review found that tooling timeout error and no compilation
failure. Existing console messages were retained, not cleared.

## Recovery and scope

`Before` preserves the exact original garment mesh/metadata and candidate prefab
/metadata. Original sources and the original `Liora_Outfits.asset` were not edited.
The first local fitting attempt is retained under Iterations/01 for provenance.

To roll back this repair without reverting later character changes:

1. Assign `Assets/_Sandbox/Characters/Liora/Candidate/Liora_Outfits.asset` back to
   the Outfit renderer.
2. Restore its **original 706-entry bone list** from `clothBonePaths` in
   binding_input.json, resolving each path against the same character root.
3. Restore only Outfit's previous bounds from the backed-up prefab, retaining its
   existing native pelvis root. Apply the same Outfit fields to live instances if
   they have overrides. Restoring the old mesh with the new 95-entry bone list is
   invalid; do not restore the entire historical prefab over later user edits.

The scene was already dirty and remains unsaved. Existing scene changes and
selection/UI references were not replaced. The saved prefab is updated; any scene
save remains under user control.

Tools under Tools/Liora: ClothingFit.cs (Audit, Metadata, Samples, Build, Install),
fit_clothing.py, preserve_fitted_outfit.py, ReviewClothingFit.cs,
ReviewActualClothingFit.cs, ValidateClothingFit.cs,
measure_clothing_validation.py and FinishClothingFit.cs. Reports are in this folder.
References consulted: integrate-metahuman-character and hierarchy-and-wiring.md;
fit-animated-character-clothing and fit-cases.md; using-unity and Unity CLI
workflow references. Historical Ethan tools were inspected and adapted to Liora's
current meshes, section manifest and native hierarchy.
