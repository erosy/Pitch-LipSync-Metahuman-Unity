---
name: fit-animated-character-clothing
description: Fix Unity character garment clipping, collar breakthrough, shoulder deformation, disappearing clothes, and seam gaps across animation poses while preserving the model, facial expressions, and rig.
---

# Fit clothing during animation

Inspect the actual active Outfit mesh, full underlying body/head, bones, bind poses,
Avatar, renderer root/bounds and the failing clip/expression combination. Read
[project fit cases](references/fit-cases.md) before adapting historical tools.
Respect AGENTS.md: compilation and disposable Edit Mode sampling, no automatic
Play Mode transitions or WebGL builds. Work on a backed-up candidate derivative.

Classify the failure before sculpting: culling/disappearance, incompatible skin
rigs, bad weights, neutral clearance, facial/head collision at the collar, or seam
discontinuity. A shirt that disappears after rootBone changes may need corrected
root-frame bounds rather than geometry. Static fit alone cannot establish motion fit.

Preserve the clip, Avatar and facial motion to reproduce the user's case. Choose
the smallest supported remedy:

- Rebind inconsistent garment/body rigs with explicit anatomical correspondence
  and preserved bind offsets; do not map neck/jaw/ear collar helpers to shoulders
  merely because they are close in space.
- Interpolate normalized body-surface weights onto local garment regions when
  their follow-through is wrong. Use closest triangle barycentric weights,
  anatomical filters and smooth spatial falloff. Process coincident seam vertices
  together. Preserve neutral placement with bind-space compensation as needed.
- Apply local bounded clearance offsets only where measured across target poses;
  report total accumulated displacement across all passes. Do not inflate the
  whole silhouette to solve a few collar points.
- Covered-body masking is outfit-specific and requires task authorization; it is
  not implied by ordinary fitting. Preserve full source body and neckline/sleeve
  boundaries, and restore the correct variant when clothing changes.

Evaluate both full-body reference correspondence and actual rendered skin,
including head/neck and facial expression deformation. Probe vertices, triangle
centers and edge midpoints; vertex clearance can miss a skin breakthrough inside
a large garment triangle. Nearest distances to an open coverage mesh are not
reliable signed penetration depths. Confirm geometric metrics with depth-tested
matched front/back/side renders.

Sample neutral and multiple phases of the failing greeting/talking clip with its
actual smile/expression, plus idle, Waving, Thinking and raised-arm activities
appropriate to the task. Reset a disposable rig to the same baseline between
samples. Expand sampling only when unresolved contact or regressions justify it.

Before installation, compare topology, UVs, material slots, expression frames and
all attributes outside the chosen region; verify finite normalized valid bone
weights, seam continuity and matching renderer bone arrays/bind poses. Protect
Face, Avatar, clips, grooms and unrelated production/scene assets. If bone-array
slots change, install the mesh and renderer bones together. Preserve user edits.

Report affected vertices, maximum **accumulated** world displacement, weight
changes, pose coverage, contact counts and visual limits. A decrease from 20 to 2
probes is improvement, not zero contact. Supply rollback instructions for the
specific mesh and bone array, not a whole-prefab restore over later user changes.
