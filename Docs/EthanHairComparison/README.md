# Ethan hair card comparison

The comparison scene starts with groom-baked hair and uses the Talk Demo camera, lights, transparent output, and Ethan speech fixture. Open [Ethan Hair Comparison.unity](../../Assets/_Sandbox/EthanHairComparison/Ethan%20Hair%20Comparison.unity) in Unity. Use the **Ethan Hair Comparison** component menu on the **Talk Demo** object to preview the cards, current ribbons, or hidden scalp hair.

The visual reference is **Hair → EthanV2_ScalpHair** with its assigned **Ethan_StrandHair** material. Unity verified all 3,021,056 control points in the bake input against the HairAsset assigned to that object. The maximum difference was **0 m**.

| Edit Mode result | EthanV2_ScalpHair | Groom-baked cards | Current web ribbons |
| --- | ---: | ---: | ---: |
| Source strands and points per strand | 94,408 × 32 | | |
| Static scalp mesh triangles | | 19,609 | 208,232 |
| Candidate mesh vertices | | 15,745 | |
| Card color and coverage atlas | | 2048 × 2048, 12 mip levels | |
| Mobile median and 95th-percentile frame time | | Pending | Pending |

The revised cards use the dense outer surface of the fitted strands to fill the crown and temples. A narrow cutout perimeter softens the opaque shell’s edge, and long card tips are constrained to the groom’s local volume. The front silhouette grew from **78% to 98%** of the groom’s visible area, with mean outline distance improving from **3.31 to 2.03 pixels** at 360 × 520. The lower edge still extends slightly past the groom in places. [Front before and after](front-shape.png) · [Measured silhouettes](silhouette.json)

The matched stills use front, 45°, and 85° camera views with spatial antialiasing. The disposable groom preview uses full strand LOD and direct drawing with subpixel discard and simulation disabled. [Light background](comparison-light.png) · [Dark background](comparison-dark.png)

## Mobile comparison

1. Use a separate Web build profile with only **Ethan Hair Comparison** in its scene list. Select **EthanHairComparison** as the Web template. Build to separate output folders for WebGL2 and WebGPU so both can be opened on the same phone. Confirm the actual graphics API in the downloaded results.
2. Open the built page, enable voice, and wait for Ethan to finish greeting. The page holds the canvas at **360 × 520 CSS pixels**. Choose a hair mode and a pixel-ratio cap of **2** or **1.5**. Changing the cap reloads the player. A phone with a lower native pixel ratio may use the same drawing-buffer dimensions for both caps.
3. Click **Run three samples**. The scene repeats Ethan’s speech fixture while applying a head turn. The harness warms up for 15 seconds, then measures three 30-second samples. Keep the tab visible and download the JSON for each configuration. Record the phone model with the results.
4. Compare the current web ribbons, candidate cards, and hidden scalp hair at the same drawing-buffer size. Repeat at the other cap and on the second API where supported. The target is a 95th-percentile frame time at or below **33.3 ms**. If only the lower cap reaches it, compare face and lip sharpness. If hidden scalp hair also falls short, the remaining scene workload needs attention.

## Regenerate

Run `python Tools/TalkDemo/bake_ethan_hair_cards.py` with NumPy, SciPy, and Pillow. It reads Ethan’s fitted 32-point groom and head distance field, then writes the candidate atlas and intermediate mesh data. In Edit Mode, use Unity Pipeline’s `run_script` with `Tools/TalkDemo/EthanHairComparisonEditor.cs` and entry point `EthanHairComparisonEditor.Generate` to import the assets and wire the scene. Use `EthanHairComparisonEditor.Review` to render the paired stills and update the [Edit Mode measurements](edit-mode.json).

Run `python Tools/TalkDemo/measure_ethan_hair_silhouette.py` after the review to rebuild the comparison sheets and measure the outline against the hidden-hair captures. To include a previous version, save its `Cards-*.png` captures in a separate folder and pass that folder as `--previous`.
