# Talk Demo frontend preview

This page is a standalone frontend/iframe integration harness. No backend calls, audio downloads, persistent storage or production frontend changes are made. JSON file contents and audio remain in memory.

## Preparing a browser test

Fast demo settings: Product Name `Talk Demo`, Compression Format `Disabled`, Decompression Fallback off, Code Optimization `Shorter Build Time`, IL2CPP Code Generation `Optimize for code size and build time`, Development Build off. Build only the enabled Talk Demo scene to `Builds/TalkDemo/`. The selected custom template has no default Unity WebGL footer. Files are larger without compression; this favors local iteration. The current active Web platform uses these global settings; the saved `Web` custom build profile has its own older Player Settings overrides, so recheck these values if you activate that profile. `TalkDemoBuildSettings.Apply` reapplies the demo defaults when no overriding custom profile is active; it never starts a build.

The Unity project selects **PROJECT:TalkDemo** as its Web template. The scene contains `TalkDemoWebBridge`, which temporarily selects the dedicated alpha-preserving URP pipeline while Talk Demo runs and restores the previous quality pipeline on disable. The existing project pipeline asset is unchanged. Talk Demo's camera clears to transparent black; studio lighting and post-processing remain in the scene.

A Web build has **not** been run. After you explicitly request or make a build, place its output at `Builds/TalkDemo/` or enter another build URL on the test page. Serve the project folder over HTTP/HTTPS and open `/WebPreview/index.html`; file:// is unsupported. Do not expose the entire project folder as a production web root.

1. Press **Load character**. Controls unlock on `bridge-ready`.
2. Tap **enable voice inside the iframe**. Replies received earlier stay queued until the actual Unity Web Audio context runs.
3. Select Ethan or Liora. Wait for `character-ready` to observe completion of warmup/waving.
4. Load the provided `unity-persona-audio-test.txt`, or paste a speech JSON object, and press **Send speech payload**. The supplied file contains tones, not voice recordings.
5. Send multiple replies and inspect `queued → speaking → completed` events. Switch personas or stop to verify cancellation.
6. Change the frontend background and resize the page. Check hair edges, transparency, framing and audio on target browsers/devices.

## Parent ↔ iframe contract

Namespace: `metahuman-talk`. Send commands with `iframe.contentWindow.postMessage(message, iframeOrigin)`. The iframe accepts only its parent window and origins listed in the built template's `bridge-config.js`. By default, only the iframe's own origin is allowed. Before cross-origin deployment set that configuration to the exact frontend origin; no wildcard origins or query-string origin overrides are used. The host also validates iframe origin and source for every received event.

```js
{ ns: 'metahuman-talk', cmd: 'select-persona', persona_key: 'Ethan Reeve' }
{ ns: 'metahuman-talk', cmd: 'speak', request_id: 'reply-1', payload: speechJsonObject }
{ ns: 'metahuman-talk', cmd: 'stop' }
```

`select-persona` also accepts `ethan` / `liora`. The `payload` for speech is the normal `{ request_id, persona_key, mime_type, audio_base64 }` object, a JSON string, or the entire supported `unity-voice-persona-test/v1` envelope. A normal reply must match the currently selected persona. The test envelope picks the selected persona's test case. See `Tools/TalkDemo/README.md` for WAV limits. Actual backend response fields belong in the frontend adapter.

Returned events use `{ ns, event_name, persona_key, request_id, message }`. Events include `bridge-ready`, `character-ready`, `audio-enabled`, `queued`, `speaking`, `completed`, `cancelled` and `error`. `character-ready` is sent once after each persona activation finishes its greeting, rather than after every speech completion. Commands sent before `bridge-ready` are rejected. All event logs exclude audio data.

The template forwards validated commands to `Talk Demo.ReceiveCommand`. The `.jslib` sends Unity events back to the parent and exposes an iframe-local audio-enable interaction. On Web, runtime audio is downmixed to mono and resampled to the device output rate for the existing lip-sync analyzer. The Web lip-sync reader bounds its sample window for short clips and uses frame offsets rather than interleaved-sample offsets.

## Source checks and limits

Run `node Tools/TalkDemo/bridge.test.cjs` from the project root for origin/source checks, readiness ordering, payload forwarding and the audio-enable flow. These tests use a mocked Unity instance. Run the relevant Unity Edit Mode tests for PCM decoding, resampling, queue behavior and audio gating.

For demo testing, both WebGL2 and WebGPU use the authored mesh/card hair replacements for Ethan and Liora. `CharacterHairFallback` requires mesh hair on the Web player regardless of compute shader support, disables strand objects and their startup warmup, and preserves character readiness through the existing fallback path. The WebGPU strand visibility override has been removed. Native/Editor strand settings remain unchanged (32 points per strand, 30 Hz). Rebuild to include this C# change; existing exports are unchanged. Verify both personas and persona switching on both browser graphics APIs after rebuilding.

For Apache/XAMPP, copy `Assets/WebGLTemplates/TalkDemo/.htaccess` next to the deployed `index.html`, including when updating an existing export. It sets the JavaScript, WebAssembly and data MIME types and sends `Content-Encoding: br` for Brotli files. To verify a local Brotli deployment, run `curl.exe -I http://localhost/talk-demo/Build/talk-demo.wasm.br` and check for both `Content-Type: application/wasm` and `Content-Encoding: br`.

## Transparent post-processing output

Talk Demo keeps its existing Volume effects and camera antialiasing. The dedicated renderer includes `TalkDemoAlphaOutputFeature`, which explicitly enables the per-camera alpha output flag used by TAA, UberPost and FinalBlit. It only targets the Talk Demo main game camera and requires an RGBA target. Both build-analyzed URP assets allow post-process alpha output so alpha shader variants survive filtering; scene-referenced pipeline inclusion remains enabled. The runtime bridge clears to transparent black to avoid background RGB leaking through soft edges.

The template loads `alpha-canvas.js` before Unity. In Unity 6000.5 the exported WebGPU canvas requests opaque compositing; the helper changes configuration only for `unity-canvas` to premultiplied alpha, including resize/recovery calls. Other canvases and WebGL are left alone. No package shader files are patched.

Run `node Tools/TalkDemo/alpha-canvas.test.cjs` for the scoped canvas configuration checks. Run `VerifyTalkDemoAlphaOutput.Run` from `Tools/TalkDemo/VerifyAlphaOutput.cs` using the Editor script runner to check that a disabled alpha flag becomes enabled without changing post-processing or antialiasing, while unrelated cameras are untouched. The Edit Mode check does not prove browser output; verify transparency in the browser after an explicitly requested build.

Rebuild after these changes and deploy the complete export, including the new `alpha-canvas.js`. Verify the iframe over both preview backgrounds under WebGPU and WebGL2, inspect transparent hair edges, and check the built canvas background alpha. Existing exports are unchanged. The source/Editor checks do not establish success of a new Web build.

Mobile WebGL follow-up: the canvas helper now preserves the rendered alpha by skipping Unity's end-of-frame alpha-only clear to one on the default framebuffer. The workaround tracks GL state without per-frame GPU queries, applies only to the character canvas, and leaves normal color, depth/stencil, transparent-alpha and offscreen clears intact. The local export was verified with WebGPU and forced WebGL2; actual mobile staging still needs verification. Deploy the updated alpha-canvas.js alongside the existing export; no Unity rebuild is needed for this JavaScript-only change.
