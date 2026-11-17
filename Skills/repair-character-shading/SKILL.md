---
name: repair-character-shading
description: Diagnose and repair black facial shading, jagged blink wrinkles, and dark speech lips in Unity characters, distinguishing texture/normal-map faults from blendshape normals, overlays, shadows, and sparse-record corruption.
---

# Repair character shading

Find the actual active prefab/mesh and reproduce the affected neutral, blink,
speech or emotion pose. Follow the project's AGENTS.md and Unity tooling skill.
Do not toggle Play Mode or make builds for this asset repair. Preserve recordings
and original assets. Complete diagnosis before choosing a material or mesh fix.

Read [the casebook and repair method](references/cases-and-preservation.md) for
the Jiwoo importer fix, Ethan head correction, sparse-ordering failure and omitted
lip sections. These are different fixes, not a single normal-map recipe.

Use disposable copies and identical pose, lighting and camera for comparisons:

1. Current lit result versus neutral and flat unlit.
2. Normal-map disabled on a temporary material copy; then shadows isolated.
3. Hair, lashes, eyeshell/edge/cartilage and separate lip sections isolated.
4. Recalculated posed geometry normals/tangents on only the suspect section.

If disabling a texture removes the defect, inspect normal texture import/type,
encoding/orientation, tangent basis, UVs, shader hookup and material strength.
Fix the demonstrated cause while preserving intended detail. If the defect remains
with the normal map disabled and posed normal recalculation removes it, repair
expression shading. Do not call that a texture-map defect or erase legitimate
creases by turning off shadows, wrinkles or facial hair globally.

Choose the smallest verified scope. Inspect **all** participating face sections:
a correct main head does not imply correct split lips, eyes or tongue. For a merged
Face derive ranges from its current section manifest and verify topology; never
copy Ethan's hardcoded offsets to a new character.

Before installation, back up the target mesh/import metadata and preserve original
sources. Stage the derivative; verify all base positions, topology, UV channels,
skin weights, bind poses, materials, shape names/order/frame weights and every
expression position array. Preserve neutral shading unless its defect is separately
established, and preserve shading outside the targeted sections. Import through
Unity and independently compare API-visible arrays. Roll back on failed guards.

Review neutral, left/right blink, brow-raised, joy, affected phonemes and mixed
speech poses. Static snapshots must evaluate shape and skin deformation explicitly
or be proven current; Edit Mode GPU captures can reuse a cached pose. Report mesh
and facial-hair counts, blendshape memory, saved/live references, checks and runtime
limits. User-controlled speaking review remains necessary when no runtime replay
was observed.
