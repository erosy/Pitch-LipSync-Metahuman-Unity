# Candidate shoulder weight adjustment

## Follow-up: greeting surface fit

The initial neutral/body-only adjustment did not resolve the user's reported
collar breakthrough. The candidate now uses **`EthanV2_Outfit_GreetingSurfaceFit.asset`**.
The earlier `ShoulderClearance` derivative is retained as the comparison baseline.

The additional inspection identified visible head/neck skin crossing the collar
during the greeting. That surface was missing from the initial body-only clearance
check. The corrected pass samples neutral plus nine positions of the **unchanged**
Waving clip with the greeting smile, considering head skin and retained body skin
separately. It locally adjusts supporting shirt weights and clearance. Triangle
centres and edge midpoints are checked as well as vertices: vertices can be clear
while skin bulges through the middle of a shirt triangle.

The final derivative moves 119 neutral shirt vertices by more than 0.01 mm relative
to the first shoulder derivative. Maximum accumulated local movement is **15.997 mm**
at the worst collar points; per-pass caps of 8 mm + 4 mm + 4 mm must not be described
as a combined 4 or 8 mm limit. The adjustment is confined to collar/shoulder areas.
There are 11 extra bone-array slots referencing existing skeleton transforms;
no skeleton transforms, animation clips, poses, facial mesh, materials, skin
coverage masks, or hair settings were changed.

`GreetingReview` contains paired before/after renders at neutral and greeting
phases 0.25, 0.375, 0.45, 0.5 and 0.75, with identical pose, camera and lighting.
The visible large patch and remaining small collar slivers are covered in the
reviewed final images. These are evaluated-mesh Edit Mode renders without scalp
simulation, not a new Play Mode run. The final triangle-probe count decreases
from 20 to 2; therefore this is not a claim of zero geometric contact everywhere.
Other activities and full runtime playback still require review.

`final_greeting_verification.json` confirms unchanged topology, all UV channels,
all 73 outfit expression frames and valid normalized weights. The live candidate
instance inherits the final mesh reference. Face and Avatar hashes remain unchanged.
No scene save or Play Mode transition was performed. Revert to the first derivative
by restoring `EthanV2_Outfit_ShoulderClearance.asset` and its original 78-entry bone
array from `Before/GreetingFit_Candidate.prefab`; avoid restoring the whole prefab
over unrelated later edits.

Tools: `FitGreeting`, `PolishGreeting`, `FitTriangleClearance` and `ValidateFinal`
in `AdjustCandidateShoulders.cs`; `RenderRevampedEthan.ShoulderReview` creates the
paired captures. Each derivative refuses to overwrite an existing result.

## Initial neutral adjustment (superseded)

Applied only to `EthanV2_Candidate.prefab`'s Outfit reference. It now uses
`EthanV2_Outfit_ShoulderClearance.asset`; the original `EthanV2_Outfit.asset`
remains unchanged for rollback. No production switch or scene save occurred.

The local pass adjusted 971 of 10,101 shirt vertices in 922 coincident-position
groups around the shoulders. A smooth spatial falloff blends nearby body-surface
weights into the existing shirt weights, up to 65%, with four normalized
influences on affected vertices. All 9,130 unselected vertices retain exact
positions, authored normals/tangents and weights. Coincident seam vertices are
processed together. No skeleton bones or bone slots were added.

The correspondence uses the preserved full-body mesh after checking its vertex
mapping against the candidate body section. Unlike the earlier inspection's
nearest-*retained*-surface check, this includes the skin triangles hidden beneath
the shirt. This distinction matters: the body coverage mesh has open boundaries,
so signed distances to its nearest retained triangle are not exact penetration
depths. Do not interpret the earlier roughly 19 mm distances as measured clothing
thickness or a required outward offset.

The selected full-body surface points already had at least 3.1655 mm signed
clearance in neutral. The 3 mm target therefore requested **zero outward offset**;
the primary correction is how the shirt follows the shoulder during deformation.
Local position compensation preserves the previous neutral placement after the
weight changes. Maximum resulting world-space displacement was 0.000121 mm,
within floating-point tolerance. The overall silhouette was not enlarged.

Data checks passed: normalized valid weights (maximum sum error 1.2e-7), unchanged
topology/UVs/materials/expression frames, and exact attributes outside the edited
region. Protected hashes confirm the corrected Face, Avatar, original shirt,
production prefab and saved CharacterTesting scene are unchanged. Body coverage
was not extended. Compilation is not failing.

As requested, visual checking across greeting and other poses is reserved for the
user. No new pose renders, animation samples, Play Mode transitions or builds
were run for this adjustment. This is not a claim of confirmed runtime resolution.

Rollback in Unity: on the candidate Outfit renderer, restore its Mesh field to
`EthanV2_Outfit.asset`. The bone array is unchanged (78 entries). The pre-adjustment
candidate prefab is also preserved in `Before`, but restoring that entire prefab
would overwrite any subsequent edits.

Tool: `Tools/EthanV2/AdjustCandidateShoulders.cs`, entry
`AdjustEthanCandidateShoulders.Apply`. It refuses to overwrite an existing
derivative or operate during Play Mode. Measurements and preservation results
are in `adjustment.json`, `affected_groups.json`, and `preservation.json`.
