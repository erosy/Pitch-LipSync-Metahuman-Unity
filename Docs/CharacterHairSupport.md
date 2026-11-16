# Shared character hair support

## Production defaults

Future character scalp hair uses **32 points per strand / 30 Hz simulation**,
Fixed strand scale, head-local meter coordinates and disabled Edit Mode
simulation, unless the user requests otherwise. Keep fitted collision boundaries
and character-specific fallback geometry. See [CharacterGroomWorkflow.md](CharacterGroomWorkflow.md).

## Startup and readiness

Attach `CharacterHairStartupWarmup` to the character root. Assign the current
Animator and explicit scalp Hair Instance, or use child discovery where needed.
Every activation clears readiness, hides the character while its pose advances,
resets strand state, then reveals it after settling. Current defaults are eight
pose-warmup frames and three frames after reset. Disabling during warmup restores
visibility, hair rendering settings and Animator culling.

`CharacterReadiness` bridges this component to the character flow controller.
Assign its startup and optional fallback references. It waits for warmup or
accepts an activated fallback. Hairless/mesh-only characters need no dummy
strand components. Active strand hair needs valid startup wiring.

Reset currently releases hair GPU buffers and initialization rebuilds them.
Preserve initial-pose safety if optimizing this later; removing readiness can
expose uninitialized or stretched hair. Warmup does not choose simulation rates
or enable simulation; set these on each Hair Instance.

## Unsupported platforms and fallback

`HairInstance` guards initialization and cleanup on APIs without compute support;
warmup skips unsupported resets. These protections must remain in the hair
package/runtime components.

`CharacterHairFallback` runs early on unsupported graphics APIs. Assign matching
mesh/card replacements under the character root, outside strand objects that
will be disabled. Assign startup warmup so it can be disabled with the strands.
The component activates existing replacements; it does not generate meshes.
Missing replacements report configuration errors rather than claiming readiness.

Ethan has a baked mesh/card fallback. Jiwoo's matching scalp fallback remains a
separate requirement before website deployment. Jiwoo's facial hair is mesh
geometry and needs no strand-surface-binding component or compute shader.

The shared components retain the original Ethan script GUIDs, migration
attributes and serialized aliases. Keep `BlenderFittedHairData`: Ethan's active
Hair Asset still depends on its custom curve provider. Keep character runtime,
Editor tooling and tests used by the production animation/expression/flow system.
