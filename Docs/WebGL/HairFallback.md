# WebGL startup crash fix

The browser console identified the first failure as `Kernel 'KRoots' not found`, followed by a `Unity.DemoTeam.Hair.HairSim` type-initialization exception, null GPU buffers, and an attempt to create a structured graphics buffer on a platform without compute-shader support. This identifies an unsupported graphics path; no heap-size increase was applied.

Changes:

- `HairInstance` rejects runtime initialization on graphics APIs without compute shaders. Its cleanup path also avoids initializing `HairSim` when disabling an unsupported instance.
- `EthanHairStartupWarmup` skips the strand reset on unsupported graphics APIs.
- `EthanWebHairFallback`, configured on Ethan in `CharacterTesting.unity`, runs early and deactivates the strand-hair object, disables the strand startup warmup, and activates the existing skinned `Hair_Cards_Baked` object when compute shaders are unavailable.
- On supported graphics APIs, the current simulated strands and startup warmup remain selected. The fallback can be previewed in Play Mode through the component's `Preview fallback (Play Mode)` context menu.

The WebGL fallback uses the existing `Ethan_HairCardsSoft` material and its baked mesh with 74 bones. It looks different from the strand scalp hair and does not have strand simulation. Facial-hair geometry, facial expression scripts, and lip-sync scripts are unchanged.

Validation:

- Desktop Play Mode initially retained one active strand-hair instance and completed the normal startup warmup.
- Activating the fallback enabled the baked mesh, removed active strand instances, disabled the strand warmup, and kept the expression controller enabled.
- The fallback material had no shader messages. No new console errors occurred during the Editor fallback check. `FallbackEditor.png` shows its appearance.
- Build result is recorded in `BuildResult.json`. A separate WebGL2 build is written to `Builds/WebGLHairFallback`; the existing `C:\Game\Metahuman Unity WEBGL` build is preserved.
- No browser automation surface was available, so the rebuilt player still needs a browser smoke test. Editor Play Mode is not a substitute for that test.

Run the new build from PowerShell:

```powershell
npx.cmd http-server "C:\PROJECTS\Metahuman Unity Lip Sync Workaround\Builds\WebGLHairFallback" -p 8081 -c-1
```

Open `http://localhost:8081/index.html`. Check startup and the Boy/Girl buttons. The old server on port 8080 continues to serve the old build.

Rollback: `Assets/_Sandbox/Scenes/CharacterTesting_BeforeWebHairFallback.unity` is a saved copy of the in-memory scene immediately before installing the fallback, including pre-existing unsaved work. Open that scene to inspect the prior state. To remove this fix from the current scene, remove the `EthanWebHairFallback` component and save; restore the added compute-support checks in the two source files only if no later work depends on them. The guards should normally remain because they prevent unsupported GPU calls.
