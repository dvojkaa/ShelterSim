# M23 wall damage art handoff

Added swap-ready presentation meshes for the common player-built wall:

| Health state | Asset ID | Triangles | Bounds |
|---|---|---:|---|
| Damaged | `piece_wall_damaged` | 808 | 2.0 x 0.2 x 3.0 m |
| Critical | `piece_wall_critical` | 428 | 2.0 x 0.2 x 3.0 m |

The damaged mesh keeps the wall closed, with cracked boards and buckled salvage plates. The
critical mesh has an actual opening through the panel while retaining the concrete frame. Both
were built through `ArtSource/build.sh` and passed the geometry bounds check. They contain no
colliders or gameplay logic.

Source: `ArtSource/assets/_m23_wall_damage.py` and the two `piece_wall_*.py` wrappers. FBX and
stable Unity importer metadata are in `Assets/Art/Models/`; four-view previews are in
`ArtSource/previews/`. IDs and geometry records are registered in `M22_ASSET_IDS.json`,
`M22_MODEL_REPORT.json`, and `M23_DAMAGE_REPORT.json`.

For gameplay integration, map these variants to the existing `piece_wall` visual at the same
health thresholds as the other damage-state art (damaged below 65%, critical below 30%). Keep
the original collider and placed-piece identity intact when swapping the renderer/model.
