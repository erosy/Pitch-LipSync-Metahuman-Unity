# Jiwoo clothing deformation audit

This is a diagnosis only. Original Blender files, imported models, materials,
prefabs, and the live character pose were not modified. No animation clips were
sampled, no Play Mode transition was made, and no build was run.

## Confirmed findings

- The delivered `Jiwoo_Rigged.blend` contains a generated Rigify rig with rig ID
  `x0bj9mk794ceaea6`, a Rigify UI script, and ORG/MCH/DEF bone families. The outfit's
  active Armature modifier targets `rig`. Preserve Volume is disabled. Another
  modifier named Root has no armature assigned and performs no deformation.
- The 5,038-vertex outfit contains 12 disconnected mesh islands. Disconnected
  pieces can be intentional; they are not by themselves proof of a defect.
- Within 0.1 mm there are 524 near-coincident vertex pairs in Unity. **505 pairs
  have different skin weights** (normalized L1 difference over 0.01). The same
  505 mismatched boundary pairs occur in the Blender source. This problem
  predates the latest prefab/model replacement.
- One right-shoulder seam vertex is weighted entirely to `DEF-shoulder.R`; its
  nearly identical counterpart is weighted entirely to `DEF-spine.004`. Several
  other pairs also have completely disjoint bone influences.
- An independent Unity check uses the actual imported outfit and its bound
  bones. On a disposable model clone, rotating one bone 20 degrees about its
  local Z axis opens near-coincident seams by up to **33.67 mm** (right shoulder),
  **34.17 mm** (left shoulder), or **31.99 mm** (upper chest). Respectively 180,
  158, and 134 mismatched pairs exceed a 1 mm gap. These are synthetic stress
  poses, not measurements of the user's animation clips.
- The saved prefab uses the intended new outfit mesh and matching bone order.
  Renderer quality is Auto, global skin weights are Unlimited, the importer
  allows 32 influences, and the outfit stores at most 7 influences per vertex.
  The large discontinuities above are present before influence truncation.
- The source has 154 clothing vertices within 1 mm of skin. There are 119
  vertices within 2 mm whose weights differ substantially from nearby skin
  (L1 difference over 0.5). This creates additional risk of body poke-through;
  nearest-surface analysis alone does not establish every actual collision.
- The outfit material is opaque URP Lit with alpha clipping off and front-face
  rendering. Backface culling can expose thin fabric from inside, but changing
  that setting cannot join separated seams.

## Repair order while retaining the current rig

1. Identify intended garment seams and make their geometry/weights continuous.
   Weld suitable coincident vertices while preserving UV/material attributes,
   or retain split vertices with identical seam positions, normalized bone
   influences, and shape-key positions. Do not blindly weld collar, armholes,
   hems, or deliberately layered pieces. Matching positions and weights makes
   seam counterparts receive the same linear skin deformation for any pose.
2. Smooth and correct garment weights around shoulders, chest, collar and waist.
   Transfer nearby body weights as an initial estimate, then review them for
   the garment's fit. Remove stray facial/forearm influences where inappropriate.
   The source currently contains small jaw/chin/ear and forearm influences on
   the sleeveless garment. Retain required deformation bones and normalize
   influences consistently across seams.
3. Keep a little geometric clearance from the body. Mask or omit body triangles
   permanently covered by this outfit as protection against skin poke-through,
   keeping a margin from neckline/armholes/hem and considering other outfits.
   A body mask cannot repair an actual opening in the garment.
4. If intended motion still produces extreme shoulder/torso distortion after
   weighting repairs, use pose-driven garment corrective shapes. Keep Rigify
   for authoring and explicitly bake/export any required constraint-driven
   behavior that the Unity Humanoid skeleton does not reproduce.
5. Add appropriate thickness or render both sides only where thin fabric's
   inside must be visible. This addresses backface visibility, not seam motion.
6. After repair, compare the disposable shoulder/chest stress checks and review
   the existing Idle/Waving/Talking/Thinking/Thumbs Up clips at representative
   frames on isolated copies. The current audit does not certify those clips.

Reports: `blender_source_audit.json` and `unity_audit.json`.
Reproduction tools: `Tools/Hair/audit_jiwoo_clothing.py` (safe background Blender)
and `Tools/Hair/AuditJiwooClothing.cs` (Unity CLI ephemeral run_script).
