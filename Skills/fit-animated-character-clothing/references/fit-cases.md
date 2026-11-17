# Project clothing cases and tools

Read these from the target MetahumanTemplate project. Verify current assignments;
later derivatives may supersede an older README's opening setup.

- `Data/EthanV2/ClothingRepair/README.md`: sleeve skin penetrated a shirt on a
  separate Rigify skeleton. Body-triangle weight interpolation with four normalized
  influences and coincident-seam consistency improved motion. A later disappearing
  shirt came from incompatible culling bounds after moving its root to a 100x rig.
  Character-root meter-space bounds restored visibility. Covered-body masking was
  explicitly authorized in that case, not a blanket permission for future fitting.
- `Data/EthanV2/Revamp/README.md`: native candidate consolidated skeletons with
  static garment bind-offset helpers; collar jaw/ear helpers needed anatomical head
  parenting. Neutral skin preservation does not imply equal retargeted motion.
- `Data/EthanV2/ShoulderClearance/README.md`: initial shoulder weight adjustment
  preserved neutral placement, but body-only inspection missed head/neck skin
  crossing the collar during smiling Waving. Greeting surface-fit probes added
  triangle centers/edges and included retained body plus head. Final accumulated
  movement reached 15.997 mm despite individual pass caps of 8 + 4 + 4 mm.
  Probe count improved from 20 to 2; static review did not establish zero contact.

Use `Tools/EthanV2/RepairClothing.cs`, `RestoreCollarBinding.cs`,
`AdjustCandidateShoulders.cs` and `RenderRevampCandidate.cs` as inspected examples.
`FitGreeting`, `PolishGreeting`, `FitTriangleClearance`, `ValidateFinal` and
`ShoulderReview` are Ethan-specific entry points. They contain hardcoded paths,
section offsets, bone arrays, clip names and overwrite guards: adapt into a new
target-specific tool rather than running the historical pipeline blindly.

Validate neutral skin equivalence through full skinning matrices, not only equal
mesh-local vertices. For changed bone identities preserve
`newBind = newBone.worldToLocal * oldBone.localToWorld * oldBind` in unchanged mesh
space; include inverse mesh-space conversion if coordinates change. Inspect weight
limits used by the actual renderer/runtime so the diagnostic follows the same
influence policy. An unweighted average of nearby bones is not equivalent to
barycentric body-surface weight transfer.

Preserve the exact baseline mesh and renderer bone list in recovery data. The
final greeting fit added eleven slots pointing to existing transforms without
adding skeleton bones; restoring its earlier mesh alone with the later array is
not a complete rollback. Do not restore the entire historical prefab over a new
gaze frame, voice wiring or other later user work.
