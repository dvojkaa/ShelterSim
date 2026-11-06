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

## 5. Test

Press Play and confirm:

- WASD + mouse look moves/rotates the player.
- You can walk through the gap in the south wall into the bunker room.
- Looking at the door logs an interact prompt in the Console.
- Pressing **E** toggles the door (it rotates visually, and open/closed state is logged).
