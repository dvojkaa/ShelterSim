# M23 outdoor fortification damage art handoff

Added same-footprint damage-state visuals for the four player-built outdoor defenses:

| Base piece | Damaged | Critical | Visual change |
|---|---|---|---|
| `piece_fence` | `piece_fence_damaged` | `piece_fence_critical` | Split rail, missing/leaning pickets, breach |
| `piece_gate` | `piece_gate_damaged` | `piece_gate_critical` | Cracked planks, broken boards and opening |
| `piece_barricade` | `piece_barricade_damaged` | `piece_barricade_critical` | Buckled boards, missing section and splinters |
| `piece_palisade` | `piece_palisade_damaged` | `piece_palisade_critical` | Broken stakes and a gap through the centre |

All eight FBXs passed the bounds build, including the rerun of the corrected damaged barricade.
Models contain no colliders or gameplay logic. The original piece collider and identity should
remain unchanged when the gameplay layer swaps their visible models.

Sources are in `ArtSource/assets/_m23_fortification_damage.py` plus one wrapper per asset. FBXs,
Unity importer metadata and four-view previews use the asset IDs above. IDs and measurements are
registered in `M22_ASSET_IDS.json`, `M22_MODEL_REPORT.json`, and `M23_DAMAGE_REPORT.json`.
Suggested visual thresholds remain damaged below 65% health and critical below 30%.
