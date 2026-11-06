# SheltreSim — Unity Setup

These scripts were written without a local Unity install available, so they haven't been compiled or run. Follow this to get M1 working.

## 1. Install Unity

- Install Unity Hub if you don't have it.
- Install a **Unity 6 LTS** release through Hub.

## 2. Create the project

- In Unity Hub, create a new project with the **3D (URP)** template.
- If Hub lets you point the project location directly at this existing folder (`/home/vojta/git/SheltreSim`), do that.
- If Hub refuses because the folder isn't empty, create the project in a temp location, then merge its generated `Assets/`, `ProjectSettings/`, and `Packages/` folders into `/home/vojta/git/SheltreSim`, **preserving** the existing `Assets/Scripts/`, `Assets/Editor/`, and `DESIGN_NOTES.md` — don't let the template overwrite those.

## 3. Confirm the Input System package

- `Window > Package Manager` → confirm **Input System** is installed (the URP template doesn't always include it).
- If prompted to restart the editor in the new input backend, accept.

## 4. Build the M1 scene

- Wait for scripts to compile with **no errors** in the Console.
- Run `Tools > SheltreSim > Build M1 Bunker Scene`.

## 5. Test M1

Press Play and confirm:

- WASD + mouse look moves/rotates the player.
- You can walk through the gap in the south wall into the bunker room.
- Looking at the door logs an interact prompt in the Console.
- Pressing **E** toggles the door (it rotates visually, and open/closed state is logged).

## 6. Build the M2 building system

- With the M1 scene open (from step 4), run `Tools > SheltreSim > Setup M2 Build System`.
- This generates two stub prefabs (`Assets/Prefabs/Building/`) and piece definitions (`Assets/Data/BuildPieces/`), and adds a `BuildSystem` child object under `Player`.

## 7. Test M2

Press Play and confirm:

- Pressing **B** toggles build mode on/off.
- A ghost piece preview follows where you're looking, snapped to a 2m grid.
- Scrolling the mouse wheel cycles between Wall Segment and Floor Tile.
- **R** rotates the ghost 90°.
- The ghost tints **green** when placement is clear, **red** when it would overlap something.
- Left-click places a real (solid) copy of the piece when the ghost is green; nothing happens when it's red.
- **Escape** exits build mode and clears the ghost.

Known rough edge (not a bug, just unpolished): a placed Floor Tile's pivot is at its center, so it slightly intersects the M1 room's floor rather than sitting flush on top — cosmetic only, fine for M2's goal of proving the build loop works.

## 8. Build the M3 survival & save system

- With the M1+M2 scene set up (steps 4 and 6 above), run `Tools > SheltreSim > Setup M3 Survival & Save`.
- This adds `SurvivalStats` to the Player, and creates standalone `HUD` and `SaveManager` objects in the scene.

## 9. Test M3

Press Play and confirm:

- Three colored bars appear top-left: health (red), hunger (brown), thirst (blue), all slowly draining.
- If hunger or thirst hits empty, health starts draining too; at 0 health, Console logs "Player has died." and stats stop changing.
- Place a few build pieces (build mode from step 7), then press **F5** — Console logs the save path.
- Move around, let stats drain further, then press **F9** — player teleports back to the saved position, stats bars jump back to the saved values, and any pieces placed after the save disappear while the saved pieces are restored.
- Save file lives at Unity's `Application.persistentDataPath` (on Linux: `~/.config/unity3d/<CompanyName>/<ProductName>/savegame.json` — exact path is logged by F5, easiest to just read it from the Console).

## 10. Build the M4 outdoor area

- With M1 (and ideally M3, for inventory save/load) already set up, run `Tools > SheltreSim > Setup M4 Outdoor Area`.
- This extends the *same* scene — no new `.unity` file, no scene-transition loading. The outdoor area sits directly beyond the bunker's south doorway.

## 11. Test M4

Press Play and confirm:

- Walking through the bunker's doorway leads into an outdoor area (ground + 3 boundary walls so you can't walk off the edge).
- Two cylinder-shaped "Wood" nodes and two sphere-shaped "Scrap Metal" nodes are scattered around.
- Looking at a node shows a gather prompt in the Console (with remaining charges); pressing **E** gathers it and logs the new inventory total.
- After 3 gathers from the same node, it logs "Depleted" instead and the next interact does nothing; the node then disappears.
- If M3 was set up first, F5/F9 also save/restore inventory counts (not just stats and pieces).
