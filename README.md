# Adaptive AR Information Panels

A Unity AR prototype in which information panels appear next to everyday objects in a
simulated room, position themselves so they stay readable, and show reminders that change
with the user's behaviour. It runs in Unity's XR Simulation, so no phone or headset is needed.

## Tools and versions

| Tool | Version |
|---|---|
| Unity Editor | 2022.3.62f3 (LTS) |
| AR Foundation (includes XR Simulation) | 5.2.2 |
| XR Simulation Environments | 1.0.0 (included in `ContentPackages/`) |
| XR Core Utilities | 2.5.2 |
| XR Plugin Management | 4.4.0 |
| Input System | 1.14.0 |
| TextMeshPro | 3.0.7 |
| Visual Studio Community (C# scripts) | 2022 |

Developed and tested on Windows. Other Unity 2022.3 versions may work but have not been tested.

## Setup and running

1. Install **Unity Hub**, then install **Unity 2022.3.62f3**. No extra modules are needed to
   run the project in the editor.
2. Get the project:
   ```
   git clone <repository-url>
   ```
   or download the repository as a ZIP and extract it.
3. In Unity Hub, choose **Projects > Add > Add project from disk** and select the project
   folder (the one containing `Assets`, `Packages` and `ProjectSettings`).
   The first launch takes several minutes while Unity imports assets and resolves packages.
   Keep the `ContentPackages` folder: the simulation environment package is loaded from it.
4. Open the scene **`Assets/Scenes/SampleScene.unity`**.
5. Press **Play**. XR Simulation is already enabled and the simulated room (**MyRoom**) is
   already selected. To check: *Edit > Project Settings > XR Plug-in Management* (PC tab,
   XR Simulation ticked) and *Window > XR > AR Foundation > XR Environment* (MyRoom).

### Controls in Play mode

Click inside the **Game** view first, then:

| Action | Input |
|---|---|
| Look around | Hold the **right mouse button** and move the mouse |
| Move | Hold the right mouse button and press **W / A / S / D** |
| Move down / up | **Q / E** |
| Move faster | **Shift** |
| Act on a reminder | **Left-click** a panel |

## Features

- **Object recognition.** Image markers on a chair photo, a plant pot and a book are
  recognised through AR Foundation image tracking (`Assets/ObjectLibrary.asset`).
- **Adaptive panel placement.** Each panel scores five positions around its object (in view,
  not blocked by walls or furniture, not overlapping other panels), moves smoothly to the
  best one, faces the viewer, and draws a line to its object.
- **Show and hide.** A panel appears while its object is tracked and hides one second after.
- **Readability.** High-contrast panels, auto-sized text, steady on-screen size, and only the
  title beyond 2.5 m.
- **Dynamic reminders.**
  - Chair: sitting timer (counts while you are near the chair and facing it), break alert
    after a limit.
  - Plant: days since last watered and when watering is next due.
  - Book: reading time towards a goal (counts while you are near the book and looking at it).
  - The title colour shows urgency: green, yellow, red.
- **Click to act.** Plant: mark as watered today. Book: stop the reading timer, click again
  to start a new session. Chair: start a break; the timer resets only after you stay away
  for the break length.

## Main files

| File | Purpose |
|---|---|
| `Assets/Script/PanelSpawner.cs` | On **XR Origin**. Creates one panel per recognised object; reminders, colours, clicks, show/hide |
| `Assets/Script/AdaptivePanel.cs` | On the **InfoPanel** prefab. Panel placement, facing, line, size and detail level |
| `Assets/InfoPanel.prefab` | The panel (world-space canvas with title and note) |
| `Assets/ObjectLibrary.asset` | Reference image library for the markers |
| `Assets/UnityXRContent/ARFoundation/SimulationEnvironments/Bedroom/MyRoom.prefab` | The simulated room with the markers |

Reminder settings (limits, distances, angles, break length) are editable on **XR Origin >
Panel Spawner** in the Inspector. Timers are set short for demonstration: chair limit
1 minute, reading goal 2 minutes, break length 30 seconds.
