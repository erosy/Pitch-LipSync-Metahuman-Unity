# Collar correction

The body-surface weight transfer raised/tightened the collar during animation, even though neutral garment geometry was unchanged. Comparing the same Idle pose using the original Rigify binding confirmed the difference. A complete return to the original garment binding opened a shoulder gap, so it was not installed.

The active `EthanV2_Outfit_CollarRestored.asset` restores the original Rigify influences only on the upper collar folds, with a smooth height/width transition. The collar base, shoulder fabric and remaining shirt retain their body binding. The source mesh vertex positions, UVs, materials, topology and all 73 channels remain unchanged. Native skeleton hierarchies are unchanged. The chest-only and wider restoration candidates were rejected.

Correct root-space visibility bounds and the user-approved covered-body mesh are retained. The original FBX and previous prefab are preserved. No runtime clothing deformer is attached.

Rendered Edit Mode checks cover Idle, Thinking and Waving; character configuration, neutral geometry, expressions and lip-sync checks pass. The open scene inherits the revised mesh. The prefab is saved; the scene remains dirty and unsaved. Play Mode was not entered or exited. Exact textured playback against the user's screenshot has not been verified in Play Mode.
