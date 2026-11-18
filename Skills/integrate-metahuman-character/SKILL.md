---
name: integrate-metahuman-character
description: Bake Blender MetaHuman facial rigs into project blendshapes and integrate a Unity character with FBX, Humanoid skeleton, grooms, modules, voice, gaze, and readiness wiring. Use for importing or replacing characters in MetahumanTemplate.
---

# Integrate a MetaHuman character

Use the current project as the source of component/API truth. Locate its root by
`AGENTS.md`, `Assets/_Sandbox/Scripts/CharacterSystem`, and `Tools/EthanV2`;
do not assume the shell starts there. Outside this project, adapt these conventions
to the target project rather than inventing missing components.

Read only the references needed for the requested stage:

- [Native facial bake and FBX](references/bake-and-fbx.md): DNA evaluation, expression semantics, coordinate conversion and skeleton consolidation.
- [Unity hierarchy and wiring](references/hierarchy-and-wiring.md): candidate structure, component references, voice, gaze and replacement checks.
- [Groom attachment](references/grooms.md): curve export, meter frame, collision and readiness.

Inspect the latest approved character and the actual target prefab before copying
anything. Historical reports are evidence of a dated state, not proof that a
candidate or source prefab is still active. Stage a separate candidate by default
for a new import; replacement scope follows the user's request. Preserve original
Blender/FBX/DNA, textures and grooms, and back up the exact assets being edited.

For Unity integration in this project, deliver the consolidated character hierarchy:
`Armature`, `Face`, `Outfit`, and `Modules`, matching the current Ethan/Jiwoo
prefab contract. Merge the visible head, body, eyes, mouth parts, brows and lashes
into `Face` material sections; retain garment geometry as `Outfit`. Preserve
dedicated scalp/fallback hair objects when actually used. Do not deliver `BAKED_*`
GameObjects, duplicate export sections, hidden authoring archives or redundant
disabled source mesh objects in the candidate/production prefab. Renaming or
grouping separate export meshes does not complete this consolidation. Original
Blender/FBX bake intermediates remain preserved outside the delivered hierarchy;
a Blender-only bake request does not require Unity integration. Separate runtime
facial meshes require an explicit user-requested exception and complete named
expression synchronization. Before delivery, reopen the saved prefab and check
all descendants, including inactive objects, for this structure and forbidden
`BAKED_*` objects; verify expressions, skinning, materials and remapped references.

Before using the Editor, follow the available Unity CLI/pipeline skill and target
this project explicitly. Do not enter or exit Play Mode: the user controls it.
Use compilation and relevant disposable Edit Mode checks; do not build WebGL unless
requested. Do not save an already-dirty unrelated scene as part of asset work.

Keep the project's production defaults: simulated scalp 32 points/strand, 30 Hz,
Fixed strand scale, head-local meters, fitted collision, readiness wiring and no
Edit Mode simulation. Mesh brows/lashes are a separate low-poly production pass,
usually 3–12 curvature-adaptive rings; preserve roots/tips, taper, skinning and
blink/brow expressions, targeting roughly 0.05–0.1 mm curve error. Report their
vertex/triangle counts and blendshape memory. Never bake every facial strand into
a 32-ring tube just because scalp simulation uses 32 samples.

Separate DNA/Alembic-derived brows and lashes that already follow the MetaHuman
facial rig in Blender are valid inputs. Preserve and verify that working binding;
do not ask the modeller to re-rig or merge these authoring objects merely because
they are stored separately.
Carry their evaluated deformation through the bake/export and include it in the
final Unity `Face`; separate authoring objects do not justify separate delivered
facial renderers. Preserve roots, weights and expression deltas during merging.

Deliver the candidate, exact reference changes, preservation checks and review
images. Distinguish numerical bake fidelity, static render review, and actual
runtime acceptance. Request user-controlled playback only after authorized
implementation and Edit Mode verification are complete.
