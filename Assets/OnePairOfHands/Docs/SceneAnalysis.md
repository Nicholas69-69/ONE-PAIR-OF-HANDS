# Scene Analysis — "Level Design"

Scene: `Assets/Skyden_Games/Low Poly Environment/Scenes/Level Design.unity`

## Root objects (before any changes)

| Root object | Notes |
|---|---|
| Directional Light | (0, 3, 0) |
| Main Camera | (-46.2, 28.8, 208.5). Has one **pre-existing missing script** component. |
| Models | Parent of 305 environment pieces. Root sits at (-82.8, -166.4, -112.4), so **children's local positions are offset by that amount** (e.g. "Tree 2  (36)" local (-35.6, 165.07, 239.9) = world (-118.4, -1.3, 127.5)). |
| GameObject | Empty object at (10.05, -15.77, 2.46) with one **pre-existing missing script**. |
| EA03_Prop_Pier_01b_PRE | EmaceArt Roadside Tales Bridges: flat bridge deck section, world centre ≈ (-58.7, -2.4, 128). |
| EA03_Prop_Pier_03c_PRE | EmaceArt Roadside Tales Bridges: sloped bridge section, world centre ≈ (-74.3, -4.9, 126). |

There is **no Terrain**. The ground is made of Skyden "Cliff 1/2/3/5/7" meshes (all with MeshColliders). Many names have double spaces, e.g. `"Tree 2  (36)"`, `"Tent 2 "`, `"Water Well "`.

## Key locations (world space)

| Thing | Object(s) | World position | Notes |
|---|---|---|---|
| Camp centre / home | `Models/Tree 2  (36)` | (-118.4, -1.3, 127.5) | On a raised plateau (`Cliff 1 (12)`, ground y ≈ -0.5), spanning roughly x -141…-99, z 116…150. |
| Ground | Cliff 1 / Cliff 2 pieces | — | Plateau y ≈ -0.5; lower west riverbank y ≈ -5.3; tent terrace y ≈ -3; east (well) bank y ≈ -2. |
| River | `Models/Lake (1)` | (-65.4, -18, 153) | Water surface 53 × 81 m in a gorge below the bridge. `Models/Water Fall ` at (-60.5, -17.6, 125). |
| Lake | `Models/Lake`, `Models/Lake (2)` | (-95.4, -7.7, 92.4) and (-210.4, -7.7, 135.3) | Large water planes (163 × 107 m). |
| Bridge | `EA03_Prop_Pier_03c_PRE` + `EA03_Prop_Pier_01b_PRE` | x -84 → -48 at z ≈ 127.5 | Deck rises from y -4.8 (west) to y -1.6 (east). The only crossing between home and the well. |
| Water Well | `Models/Water Well ` | (-39.1, -2.2, 131.7) | East bank, across the bridge from home. |
| Tent 1 | `Models/Tent 1 ` | (-92.3, -3.1, 182.3) | Tent terrace north of the bridge. |
| Tent 2 | `Models/Tent 2 ` | (-84.5, -3.2, 176.7) | Tent terrace. **Closest to home** (see below). |
| Other tents | Tent 1 (1)-(4), Tent 2 (1)-(4) | (-83…-173, z 161…185) | (2)-(4) group sits far west on high cliffs. |
| Houses | — | — | **None in the scene.** Skyden prefabs House 1–5 and Warehouse01 are available (no caravan prefab exists in the pack). |
| Stairs | `Models/Stairs ` | (-125.4, -5.3, 157.1) | Links the home plateau down to the lower level, north side. |
| Cliffs / mountains | Cliff 1–7, Mountain 1/2 | mostly z < 100 and x > -30 | Background; ignored (outside the playable NavMesh volume). |
| Props | Cart01 ×2, Fence01/02/06/07, Doghouse, Basket01/02 | south of the lake (z 17–74) | Outside the playable area. |

## World scale

- Home → Water Well, straight line: **≈ 80 m**.
- Home → Water Well, on foot (NavMesh, via stairs and bridge): **≈ 71–79 m** each way. At walking speed 3.5 m/s that's about 22 s each way, roughly 45 s of real time per round trip (≈ 45 in-game minutes on the default 8-minute day).
- Home → Tent 2 on foot: ≈ 70–73 m. Home → Tent 1 on foot: ≈ 76 m.
- Straight-line: Tent 2 59.7 m, Tent 1 60.7 m. **Tent 2 is closer by both measures**, so it's used for "Teach the children".

## Walkability issues found (and how they were solved)

1. **Plateau → riverbank**: a ~5 m drop with no walkable link on the bridge side. Added a second Skyden `Stairs` prefab (`OnePairOfHands/Environment/Stairs_HomeToBridge`) on the plateau's east edge.
2. **Riverbank → tent terrace**: a ~1.8 m ledge. Added a scaled Skyden `Stairs` (`Stairs_BankToTents`).
3. **Bridge deck**: the EmaceArt plank mesh has gaps and seams that break the NavMesh. Added two invisible box colliders that follow the deck (`BridgeWalkway_Ramp`, `BridgeWalkway_Deck`); they're NavMesh helpers only, with no renderer.
4. **Water**: lake, river and waterfall surfaces were walkable. Added `NavMeshModifier` (area = Not Walkable) to `Lake`, `Lake (1)`, `Lake (2)` and `Water Fall `. Their transforms are unchanged.

No existing environment objects were moved or deleted. `Tree 2  (36)` was **disabled** (not deleted) to make room for the camp.
