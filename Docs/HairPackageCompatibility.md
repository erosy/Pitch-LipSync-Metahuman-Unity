# Demo Team Hair: Unity 6.5 compatibility

The project embeds Demo Team Hair 0.19.0-preview.1 in
`Packages/com.unity.demoteam.hair`, based on upstream commit
`75a7f446209896bc1bce0da2682cfdbdf30ce447`. Keep this directory in source control;
the package contains local fixes and no longer depends on the Git package cache.

Unity 6.5 makes `Object.GetInstanceID()` and `RenderParams.instanceID` compile
errors. Rendering now assigns `RenderParams.entityId` from `GetEntityId()`.
For the simulation's 32-bit handles and packed group sort keys,
`HairObjectIdentity.GetHandle` assigns a unique integer to each full `EntityId`.
It never truncates an entity ID or substitutes its hash code for identity.
The mapping lasts until domain reload, retains IDs for objects already destroyed,
and never reuses handles while simulation or cached data may reference them.
Older editors retain the original instance-ID APIs through version guards.

Verification: the runtime, editor, and experiments assemblies compile with the
installed Unity 6000.5.9f1 Roslyn compiler, using the project's Bee response files
and the patched runtime reference assembly. No C# errors or warnings were emitted.
Live editor refresh and play-mode behavior still require an editor check.

When updating the upstream package, preserve or reapply these compatibility
changes until upstream supports Unity 6.5.

Prefab Mode visibility: indirect hair and shadow draws explicitly use
`EditorSceneManager.GetSceneCullingMask(gameObject.scene)` in editor builds, and
use the Hair Instance's GameObject layer. This associates the procedural draws
with their scene rather than relying on the default scene mask. The runtime
assembly compiles after this change; Prefab Mode visibility needs visual
verification in the editor.
