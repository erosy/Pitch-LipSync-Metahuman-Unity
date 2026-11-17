# Blender groom to Unity attachment

Use the installed Unity DemoTeam Hair package and the project's data provider;
inspect current APIs before adapting `Tools/EthanV2/export_scalp32.py`,
`InstallScalpGroom.cs`, `VerifyScalpGroom.cs` and `VerifyRevampCandidate.HairChecks`.
Those examples hardcode Ethan counts, paths, source ordering and coordinate axes.

1. Identify the real scalp groom rather than brows/lashes or archived duplicates.
   Preserve raw curves and world matrices. Match any Alembic source to the approved
   Blender groom using measured correspondence; equal counts do not prove equal
   strand order. Record source hashes, units, handedness and alignment error.
2. Convert through Blender world space into a normalized Unity head-local meter
   frame. Derive the transform from the actual imported head; do not reuse Ethan's
   axis formula blindly. Arc-length resample simulated strands to **32 points**,
   preserving each root/tip exactly and handling degenerate curves explicitly.
3. The existing provider format uses little-endian int32 magic `0x48414952`, strand
   count, points-per-strand, followed by float32 XYZ positions. Verify the current
   `BlenderFittedHairData` reader before producing bytes. Build a custom HairAsset
   with pre-resampled data and avoid resampling again. Read memory layout through
   `HairAssetUtility.DeclareStrandIterator`; do not assume contiguous strands.
4. Attach hair beneath the mapped head in a frame with world scale one. If native
   hierarchy scale includes 100x, use a calibrated meter proxy, not a second global
   scaling of the curves. Verify root/tip placement in world space after build,
   parent changes and sampled head rotations. Retain surface attachment/binding
   references to the actual skinned Face/head submesh if the package uses them.
5. Build a fitted head/ear/neck collision boundary in the same frame. For the
   existing discrete SDF, verify sign, voxel order, bounds, world size, orientation
   and padding against the actual head; Ethan's 96³ volume is an example, not a
   universal requirement. Use an explicit resident boundary and inspect whether
   automatic boundary capture would add unintended scene colliders.
6. Set simulation to **30 Hz**, Fixed strand scale, disabled Edit Mode simulation.
   Preserve approved material, strand dimensions, solver/volume settings unless
   the requested fit needs an explained adjustment. Wire startup warmup to the
   actual Animator and groom, readiness to warmup, and flow to readiness.

Check saved and live references, 32-point layout, all root/tip errors, local meter
coordinates, unit world scale, collision alignment and startup behavior. Fit and
motion come before density/frequency increases. Static mesh previews omit hair
simulation and cannot establish settling or runtime collision quality; report that
limit and leave Play Mode transitions to the user.
