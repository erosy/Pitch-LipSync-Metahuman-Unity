# Project verification preferences

## Character hair production defaults

Use **32 points per strand** and **30 Hz simulation** for all future character
hair models and groom integrations unless the user explicitly requests otherwise.
Preserve root/tip positions, head-local meter coordinates, Fixed strand scale,
fitted head collision boundaries, and startup/readiness wiring. Keep Edit Mode
hair simulation disabled. Validate fit and motion before increasing density or
simulation frequency; do not silently restore 64-point/60 Hz settings.

### Mesh eyebrows and eyelashes

The 32-point/30 Hz default applies to simulated scalp strands, not to tubular
mesh facial hair. Use curvature-adaptive low-poly brows/lashes, typically 3–12
axial rings per strand, rather than baking every strand as a 32-ring tube into
all facial blendshapes. Preserve roots, tips, thickness/taper, skin weights,
materials, expression names and blink deformation. Check world-space curve
error (aim for about 0.05–0.1 mm) and neutral/blink/brow-raised comparisons.
Report facial-hair vertex/triangle counts and blendshape memory before delivery;
do not accept a fidelity-first high-density conversion without a production pass.
Keep dense authoring geometry outside the active production mesh. Remove only
provably redundant body shape/tangent data, after checking all facial bindings.

## Unity verification

Do not run WebGL builds unless the user explicitly requests a build. For routine
implementation and cleanup, use compilation and relevant Edit Mode checks.

Do not enter or exit Unity Play Mode automatically. The user controls Play Mode.
