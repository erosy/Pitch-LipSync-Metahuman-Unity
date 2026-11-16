# Jiwoo prepared with Ethan's facial interface

Open `Jiwoo_EthanSetup.blend` in Blender 5.2 or newer. Embedded scripts do not
need to be enabled. This is a separate review/export copy; the original Downloads
file and all existing Unity assets remain unchanged.

`Jiwoo_EthanSetup.fbx` is retained here as the export artifact. A copy is now
imported under `Assets/_Sandbox/Characters/Jiwoo New` and used by the original
`Jiwoo_Normalized.prefab`. The older `Jiwoo_Rigged.fbx` file is retained but is no
longer referenced by that prefab. See [replacement notes](PrefabReplacement/README.md).

## What matches Ethan

Ethan's actual imported `Ethan_unity.fbx` supplies the exact ordered 73 facial
shape names. Jiwoo now has a combined `Face` mesh, a separate `Outfit`, and a
neutral 160-bone deform skeleton. Her brows and lashes are mesh strands inside
`Face`, with their own material, skin weights, and all matching facial shapes.
They no longer need a separate runtime facial groom binding in this candidate.
The scalp remains a separate Blender curve groom, excluded from the staged FBX.

The combined Face contains 127,026 vertices, including 45,960 eyebrow vertices
from 1,915 strands and 18,888 eyelash vertices from 787 strands. Facial strands
use eight samples and three sides with tapered tips. They are separate mesh
islands within the same skinned mesh, not welded into the skin surface. Their
roots follow barycentric skin triangles; each sampled expression also rotates
the strands with the local skin surface.

The 92 matching body/head boundary vertices retain the seam repair: identical
positions in every shape and identical normalized skin weights. An unused,
unlinked original lash-card object is not included in the prepared character.

## How the facial shapes were made

These shapes replace the earlier Rigify approximations in the older prepared
copy. The generator explicitly evaluates Jiwoo's own `head.dna`, found under
`C:/Users/ina17/Downloads/Jiwoo_DNA_MetaHumanCharacter`, using the installed
Character DNA RigLogic SDK. It applies native joint outputs through the
delivery's saved joint conversion plan and includes DNA corrective shape
deltas, then bakes the evaluated result onto the neutral export skeleton.
The embedded `rig_ui.py` and driver solver expressions are never executed.

The main head matches the DNA's neutral vertex positions within 0.00008 mm.
The modeller's welded teeth and separated inner-mouth pieces require a looser
correspondence tolerance of 0.2 mm; their original offsets are retained.
`preparation_report.json` records the actual errors, controls, movement, source
hashes, mesh sections, and seam pairs.

The **names and mesh organization** match Ethan. His original control settings
and facial clips cannot be recovered from his FBX. Visemes and emotions are
explicitly authored combinations of Jiwoo's native controls; they still need
artistic review. These are sampled expression endpoints, not a recovered
animation sequence or the full nonlinear MetaHuman solver. Combining linear
shape keys can differ from evaluating the DNA with combined controls.

## Review in Blender

The visible `JIWOO_ETHAN_SETUP - Face and mesh eyebrows` collection contains the
prepared character. The hidden `SOURCE_AUTHORING - preserved, hidden` collection
retains the authoring scene. Keep it hidden to avoid duplicate characters.

Select `Face`, open Object Data Properties > Shape Keys, and move one key from
0 to 1. Return it to 0 before reviewing another. `viseme_sil` is intentionally
neutral. The outfit also retains the synchronized shape interface.

Useful checks are both blinks, brow raises/down, smile, jaw opening, lip closure,
and FF/TH visemes. Inspect eyelid contact, lip seals, teeth/tongue intersections,
and combinations. Do not layer equivalent facial bone animation over these
baked shapes. Neutral skeleton motion can still be retargeted separately.

`review_*.png` are disposable clay geometry previews with scalp hidden and
texture shading disabled. They are for expression and attachment review,
not final material/eye rendering.

## Reproduction and verification

Generator: `Tools/Hair/prepare_jiwoo_ethan_reference.py`, run in background Blender
5.2 with `--factory-startup --disable-autoexec` and the original Jiwoo delivery
open. It refuses to overwrite the prepared file by default. The developer-only
`-- --rebuild-generated` option overwrites generated output; do not use it after
artist edits unless those edits have been saved elsewhere.

`Tools/Hair/verify_jiwoo_ethan_reference.py` independently reopens the original,
prepared Blender file, and exported FBX. It checks the neutral silhouette, all
73 shape positions and evaluated results, normalized weights, 160 bones, mesh
facial-hair bindings for every shape, and the repaired body/head seam.
`verification_report.json` records the results when those checks pass.

Saved-file and independent FBX readback checks passed for all 73 shapes on both
meshes and all 160 bones. Maximum FBX shape-position error is 0.00157 mm;
maximum evaluated Blender error is 0.00036 mm. The prepared neutral silhouette
differs from a fresh source evaluation by at most 0.00291 mm. Facial strand
surface-following error is below 0.000061 mm across all shapes, and the repaired
92-vertex body/head seam has zero position gap and matching weights.

The initial Blender preparation did not modify Unity assets. The subsequent
Unity replacement preserves material remaps, adds `Jiwoo_FacialHair_Mesh`, and
removes all old scalp/eyebrow/lash groom objects and runtime groom support for
now. The Humanoid/avatar configuration passed static checks. No Play Mode
transition, animation playback, or build was performed.
