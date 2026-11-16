# Ethan V2 mesh eyebrows

Production override: the V2 prefab now uses the optimized head mesh described in
[FacialHairOptimization](../FacialHairOptimization/README.md). The dense FBX and
Blender sources documented below remain unchanged as authoring/recovery sources.

The active `Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2_73_Baked.fbx` now includes
the delivered eyebrows as mesh islands inside `BAKED_head_lod0_mesh`. Its model
GUID and previous material remaps are preserved. `Ethan_Eyebrows.mat` is the new
second head material. The previous FBX and importer metadata are under `Before`.

Editable source: `Ethan_V2_73_WithEyebrows.blend`. Keep the archived source
collection hidden. The scalp curve groom remains in Blender but is excluded
from FBX. The existing lashes have not been converted or replaced.

All 2,804 delivered brow strands are retained, resampled to 32 positions each
with original roots/tips preserved. A tapered three-sided mesh cross section
uses a 0.25 mm diameter through the first 30 samples, tapering to 0.05 mm at
the tip. This replaces the initial 0.09 mm strongly tapered version to better
match Blender's visible coverage. The original groom's radius profile is
constant; its stored radius does not directly match mesh rendering. This width
is a visual adaptation rather than a literal copy of that radius value.
There is no eyebrow simulation. The brow section adds
269,184 vertices; this is a fidelity-first mesh conversion and may warrant
later optimization. Unity's imported head has 289,411 vertices including seams.

Roots bind barycentrically to the head triangles. Each of the 73 expressions
translates and rotates the strand with its surface frame. Skin weights are
interpolated from the same root triangle. Since the brows are inside the head,
the existing head sliders animate them directly without another runtime driver.

The staged FBX exports the 13 baked character meshes and their armatures. Hidden
authoring duplicates and control widgets from the earlier manual export are
excluded. No scalp integration, Unity Play Mode transition, or build was run.

See `fbx_verification.json`, `unity_installation.json`, and neutral/raised-brow
clay images under `Previews`. This is geometry validation; review the eyebrow
color and density under the scene lighting. Existing scene material overrides
may need their new second head slot set to `Ethan_Eyebrows`.

`BeforeThickness` preserves the first mesh-brow source, exported FBX, installed
Unity FBX and importer metadata. Strand centerlines and all facial shape centers
are unchanged by the thickness revision. No strand-count increase was made.
