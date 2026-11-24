# Talk Demo — Batches 1–4

The standalone browser host and iframe contract are documented in `WebPreview/README.md`. The configured scene, runtime assets, tests, build settings helper and read-only verification tools are retained. One-time setup and diagnostic scripts have been removed.

Open `Assets/_Sandbox/Scenes/Talk Demo.unity`. The `Talk Demo` root holds the controller and both candidate prefab instances. Ethan is the initial selection. The source candidate prefabs and the existing studio lighting are preserved.

## Manual check

1. Enter Play Mode yourself. Ethan should finish hair warmup, wave silently once, then remain idle.
2. Select the `Talk Demo` root. In its Inspector use **Select Liora** and **Select Ethan**.
3. Check chest-up framing, facial appearance, and hair during warmup, waving, and idle.
4. Select the same character again: it should not repeat the wave.
5. Switch during a wave: the previous character should disappear and the new one should warm up and wave once.
6. On the same root, find **Talk Demo Speech Input** and press **Send Selected Persona Fixture**. The character should speak with lip sync, then return to idle. These are the first 12 seconds (or less) of the existing local voice clips.
7. Send several fixtures while one is playing. They should play in order. Also send one during the initial wave: it must wait until the wave ends.
8. Switch personas during playback. The current voice must stop and all pending replies must clear. Send the new persona's fixture and check its voice and lip sync.
9. Press **Stop And Clear Speech** to cancel playback and the queue while keeping the current persona in idle.
10. Stop Play Mode yourself. No thinking animation is expected.

The controller Inspector shows Selected Persona Key, State, and Last Error. Edit the Initial Persona Key before Play Mode to start with `Liora Minji` instead of `Ethan Reeve`.

## Speech JSON contract

For the `unity-voice-persona-test/v1` test envelope, paste the complete JSON into **Test Json during Play Mode** and press **Send Test JSON**. The input selects `test_cases[].audio.audio_b64` for the current persona (`ethan` → Ethan Reeve, `liora` → Liora Minji); it does not switch characters. Select the other character and send again to preview its case. The supplied one-second synthetic tones test delivery/playback, not speech quality. Incoming audio is decoded in memory and is never written to a file. Only the optional local demo fixtures are stored assets.

Call `TalkDemoSpeechInput.TryEnqueueSpeechJson(json, out error)` or the void `ReceiveSpeechJson(json)` entry point on the `Talk Demo` object. The latter is ready for a future Unity browser `SendMessage` bridge. You can paste JSON into **Test Json** in the Inspector and press **Send Test JSON**.

```json
{
  "request_id": "reply-001",
  "persona_key": "Ethan Reeve",
  "mime_type": "audio/wav",
  "audio_base64": "<Base64 of the complete WAV file>"
}
```

Use the exact persona keys `Ethan Reeve` and `Liora Minji`. Speech does not implicitly select a character. The Base64 value must be the file bytes without a data-URL prefix. Supported audio is complete RIFF/WAVE, uncompressed PCM16, mono or stereo, 8–96 kHz, at most 60 seconds and 16 MiB per file. Sample rate and channels are preserved.

Eight pending replies are accepted in arrival order, in addition to a playing reply. Full queues, invalid audio and persona mismatches are rejected without replacing accepted replies. A persona switch clears the current voice and queue; the same selection does not. WAV decoding is synchronous on the Unity main thread, so there are no deferred decode callbacks that can restart an old persona. Runtime clips are created only when a reply reaches the front of the queue and released on completion/cancellation/failure. Bad playback returns to idle and the next queued reply can continue.

`SpeechEvent` emits `queued`, `speaking`, `completed`, `cancelled` and playback `error` notifications with request ID and persona. Input validation errors are returned by the boolean API and shown in Last Error. The frontend adapter will map the actual backend response field names to this contract in a later batch.

## Verification

`TalkDemoControllerTests` exercises readiness, wave completion, selection, FIFO speech, cancellation, queue limits, failure recovery, and the public Edit Mode guards without entering Play Mode. `Pcm16WaveTests` checks PCM values, channels/rates, RIFF padding, truncation, unsupported formats and duration. Existing character-flow tests cover assigned-clip compatibility. `VerifyTalkDemo.Run` checks the configured scene, speech bindings/fixtures, silent startup, prefab sources, framing, and 32-point/30 Hz scalp hair with Edit Mode simulation disabled.

## Hair fallback

Each candidate root now has **Character Hair Fallback**. In your own Play Mode session, select that root and press **Preview Mesh Hair**. Press **Restore Strand Hair** to return to strands on supported hardware; restoration runs the existing hair warmup. On hardware without compute-shader support, mesh hair activates automatically and strand restoration is disabled. This is a capability check, not a frame-rate monitor or a guarantee of WebGPU compatibility.

The mesh replacement follows the head bone. It is static hair, without strand dynamics. Both scalp replacements use 12,000 sampled source curves, curvature-adaptive ribbons, preserved centerline endpoints, and at most 0.35 mm simplification error against the source samples. Ethan has 232,232 vertices / 208,232 triangles; Liora has 373,668 vertices / 349,668 triangles. Original simulated grooms, collision boundaries, 32-point/30 Hz settings, source prefabs and facial meshes remain intact.

WebGPU now uses the strand path again when compute support is available; the forced mesh override has been removed. `HairVertex.hlsl` guards zero guide reach before evaluating exponential coverage. For the smaller iframe, `TalkDemoWebBridge.Awake` removes the subpixel cutoff on the demo's WebGPU hair instances. This renders more strands at small sizes without changing groom density, width, points or simulation frequency. Equivalent shader changes restored both personas in an isolated hardware WebGPU iframe experiment. Rebuild and test the generated shader on each target device. WebGL2 retains the automatic mesh fallback.

Shader import cleanup: the `HairVertex` subgraph treats `PROCEDURAL_INSTANCING_ON` as a predefined keyword, since the procedural instancing pragma already supplies its variants. The unused volume raster shader excludes WebGPU; `HairSim` already routes that backend through compute splatting.

Facial-hair material-section counts (existing geometry retained):

| Character / section | Vertices | Triangles | Dense blendshape equivalent, MiB |
|---|---:|---:|---:|
| Ethan brows | 75,210 | 133,596 | 193.66 |
| Ethan lashes | 9,195 | 15,138 | 23.68 |
| Liora brows | 24,063 | 38,568 | 60.31 |
| Liora lashes | 11,190 | 17,484 | 28.05 |

The last column estimates full position/normal/tangent arrays for every frame; it is not allocated sparse blendshape memory. Unity reports 155.27 MiB for Ethan's entire facial mesh and 255.35 MiB for Liora's entire facial mesh, including all sections and shape data. No additional facial blendshape data is created by this batch. These existing counts are not a certification of mobile performance.

Generated reports, static previews and browser probe captures are excluded from source control. Test idle, wave, speech, blinking and both preview controls on the target devices. Static renders do not establish animated hair motion or browser acceptance.
