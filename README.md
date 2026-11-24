# Nucleus Kinematic Character Controller

**[Kinematic Character Controller](https://assetstore.unity.com/packages/tools/physics/kinematic-character-controller-99131) characters predicted by the player who controls them and checked by the server every tick, on the Nucleus network engine, with KCC itself left exactly as the Asset Store ships it.**

The player's client moves its character the moment a key is pressed. The server runs the same KCC simulation from the same input, and when the two disagree the client is put back on the server's state and replays its newer ticks. Moving platforms are predicted with the character, so riding one needs no correction. Everyone else's character is drawn from the server's state.

## Requirements

- Unity 6000.6 (built and tested on 6000.6.4f1), Built-in Render Pipeline.
- Nucleus with its Unity integration in `Assets/Nucleus`.
- Kinematic Character Controller 3.4.4, imported from the Asset Store. The integration and the demo use only KCC's `Core`.

## Install

1. Import KCC from the Asset Store.
2. In the Package Manager, choose **Install package from git URL** and enter `https://github.com/FirstGearGames/Nucleus-KinematicCharacterController.git`. Add `#v1.1.2` to the end to pin a release. Or copy this repository into the project's `Packages` or `Assets` folder.
3. Wait for the editor to recompile. KCC ships with no assembly definitions, so its scripts compile into `Assembly-CSharp`, which a package cannot reference. On load, this package adds `KinematicCharacterController.asmdef` to KCC's `Core` folder and `KinematicCharacterController.Editor.asmdef` to `Core/Editor`, and adds `NUCLEUS_KCC` to the Scripting Define Symbols, which switches the rest of the package on. KCC's own scripts are not changed, and its examples still compile.
4. Open **Tools > Nucleus**, Build tab, and run **Rebuild Network Prefab Collection**.
5. Add `Demo/Scenes/NetworkKccDemo.unity` from this package to the build settings.

## Try the demo

Open `Demo/Scenes/NetworkKccDemo.unity`, press Play and click **Start Host**. For a second player, open a ParrelSync clone, press Play there and click **Start Client**; it connects to `127.0.0.1` on port 7777.

| Input | Does |
|---|---|
| W A S D or the arrow keys | Walk, relative to the camera |
| Mouse | Look; left click locks the cursor again |
| Space | Jump |
| C, held | Crouch; the character stands again once nothing is above it |
| Scroll wheel | Zoom the camera |
| Return | Fire the cube launcher; the server fires it, so every player sees the same cube |

The scene is built from primitives: a ramp up to a raised block, a slope too steep to stand on, stairs, a tunnel only a crouched character fits through, four moving platforms (a slider, a lift, a spinner and a loop), a teleporter pair from the ground to the top of the block, five cubes the server simulates and the launcher.

## How it works

KCC normally steps every motor and mover itself in `FixedUpdate` and smooths them in `LateUpdate`. A scene's `NetworkKinematicCharacterSystem` turns both off and steps KCC on the Nucleus tick instead:

1. On each tick's early fixed update, every system this peer simulates enrolls its motor or mover. The server simulates every character; a client simulates the character it controls and every mover.
2. Once every early fixed update has run, one `KinematicCharacterSystem.Simulate` steps the enrolled set in KCC's own order: mover velocities, every motor's first phase, mover moves, every motor's second phase. That order is what carries a character on a platform by the platform's move of the same tick.
3. On the late fixed update, `ValidatedKinematicCharacterMotor` records the motor's whole state, KCC's `KinematicCharacterMotorState` plus the capsule size, as members the server checks.
4. On a mismatch Nucleus restores the client to the server's state for that tick and replays every tick since. Replayed ticks step KCC from the same place, so a replay is the same simulation.

Movers are predicted ahead of the server by every client. `ValidatedPhysicsMover` keeps a tick clock, `SimulationTime`, that the mover's controller reads instead of `Time.time`; a client runs it on its own ticks, and each time the server's mover state arrives the client checks it against its prediction for the same tick and re-anchors on a difference. A mover whose path is fixed, such as `NetworkPathMover`, works its pose out from that clock alone, so it lands where the server will have it when the client's input arrives.

Characters and movers are drawn through Nucleus's `GraphicalSmoother`, which slides the drawn pose across each tick, so the 30 tick per second simulation looks smooth and a correction never snaps. The colliders and KCC's math always stay at the true pose.

## Use it on your own character

1. Add one `NetworkKinematicCharacterSystem` to the networked scene, beside the `UnityCoreManager`.
2. On the character prefab, beside its `KinematicCharacterMotor` and your `ICharacterController`, add `NetworkSystemObject`, `ValidatedKinematicCharacterMotor` and a `GraphicalSmoother` whose **Child Graphic** holds the meshes and the camera target.
3. Write one `NucleusBehaviour` for your controller, as `NetworkDemoCharacter` does for `DemoCharacterController`:
   - input as `PredictionRole.Predict` members, written on the controlling player's live tick, with presses declared `isCarriedForward: false`;
   - your controller's own state as `PredictionRole.Validate` members, recorded on `OnLateFixedUpdate` and put back in `OnReplayStarting`;
   - the input handed to the controller on `OnEarlyFixedUpdate` of every simulated tick, replayed ones included.
4. On every moving platform add `NetworkSystemObject`, `ValidatedPhysicsMover`, a `GraphicalSmoother` with Child Graphic off, and an `IMoverController` that reads `ValidatedPhysicsMover.SimulationTime` or the mover's own state, never `Time.time`. `NetworkPathMover` does this for a fixed path.
5. Decide anything that changes how a character moves, such as a teleport, inside the tick from the motor's own pose. Trigger callbacks cannot be repeated by a replay; `NetworkTeleporter` and `NetworkDemoCharacter` show the pattern.

## Folders

| Folder | Holds |
|---|---|
| root | The integration: `NetworkKinematicCharacterSystem`, `ValidatedKinematicCharacterMotor`, `ValidatedPhysicsMover` and their components |
| `Editor` | Inspectors, and in `Editor/Setup` the step that gives KCC its assembly definitions |
| `Demo` | The original demo: character, player, camera, path mover, teleporter, launcher, HUD, scene, prefabs and materials |
| `Tests` | Live two-editor tests, a replay check for the demo controller and checks of the path mover |

## Tests

`Tests/KinematicCharacterLivePlayTests.cs` plays the demo between two editors over UDP. Each scenario is a pair: run `Server_<Scenario>` in the main editor's Test Runner (Play Mode) and `Client_<Scenario>` in a ParrelSync clone. The two meet through files in `%TEMP%/kcc-live-tests`, or the folder in `KCC_TEST_DIRECTORY`, on port 47781 unless `KCC_TEST_PORT` names another. Either side run alone times out. Installed from git, the tests show in the Test Runner only when `com.firstgeargames.nucleus.kinematiccharactercontroller` is listed under `testables` in `Packages/manifest.json`.

| Scenario | What it plays | What must hold |
|---|---|---|
| Walk | Walk, jump twice, strafe while crouched, walk back, then fire the launcher | No reconcile and no server mismatch once settled; both end within 2 cm; the launched cube reaches the client |
| Lossy | The same over 60 ms latency, 10 ms jitter and 5% loss | The measured round trip is at least 60 ms; both end within 2 cm |
| Correction | A short walk, then the server moves the standing character 1.5 m aside | The client is reconciled, takes the move and ends within 2 cm of the server |
| Platform | Spawn on the slider and ride it | No reconcile and no server mismatch once settled; still on the slider at the end |
| Teleport | Spawn inside the ground teleporter | Both sides arrive at the teleporter on top of the block |

All five pass. `DemoCharacterControllerReplayTests` steps KCC by hand and fails when the controller's state is not put back before a replay; the live tests cannot time a correction that finely.

## Things that look like bugs but are not

- **Nothing in this package compiles, and there are no errors.** `NUCLEUS_KCC` is not set yet, or KCC is not imported. The setup sets it once it finds KCC's `KinematicCharacterMotor.cs`. If KCC already has an assembly definition under another name, the console says so: rename it to `KinematicCharacterController`.
- **Errors after removing KCC.** Remove `NUCLEUS_KCC` from the Scripting Define Symbols as well.
- **A client corrects once or twice in its first second.** Its first copy of each mover arrives a round trip old, and the first comparison with the server re-anchors it.
- **Other players feel slightly behind when you bump into them.** Only your own character and the movers are predicted; other players are where the server last said.
- **A teleporter fires a tick after you step into it.** The character tests its capsule against each teleporter at the start of every tick, so the server and every replay teleport on the same tick.

## Branches

- `main`: this integration with the original demo.
- `original-conversion`: the first conversion of KCC's own CharacterPlayground, kept as a private backup. It is built from KCC's example folders, so it is not to be published without the author's permission.
