# Ecosystem Choices - XR Locomotion & Ecological Simulation Prototype

[![Unity 6](https://img.shields.io/badge/Unity-6000.5.0f1-blue.svg)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/URP-Universal%20Render%20Pipeline-brightgreen.svg)](https://unity.com/srp/Universal-Render-Pipeline)
[![Platform](https://img.shields.io/badge/Target-Meta%20Quest%20%7C%20OpenXR-0082FB.svg)](https://www.meta.com/quest/)
[![License](https://img.shields.io/badge/License-MIT-lightgrey.svg)](LICENSE)

An immersive Virtual Reality (VR) prototype designed for Meta Quest and OpenXR headsets. **Ecosystem Choices** merges physics-driven VR locomotion and trail constraint systems with dynamic, procedural biodiversity simulations (avian flocking, butterfly flight mechanics, and ground insect trail crossings), empowering participants to explore natural environments and observe ecological dynamics.

---

## 📌 Project Overview & Scope

The prototype delivers a **comfort-focused, highly responsive VR locomotion framework** combined with an **autonomous wildlife biodiversity simulation engine** and **interactive environmental Point of Interest (POI) observation zones**.

### Key Features & Capabilities

1. **Comfortable Continuous Locomotion**: Thumbstick-driven translation with customizable radial deadzones, slope speed reduction, and automated terrain ground alignment.
2. **Path & Trail Constraint System**: Keeps participants naturally guided along designated walking trails using high-performance painted terrain splatmap sampling (`PathMovementConstraint`) with smooth edge repulsion and obstacle avoidance.
3. **Head-Centered Rotation**: Offers both **Snap Turn** (increments: 15°, 30°, 45°, 60°) and **Smooth Turn** (60°–90°/s) pivoting around the user's physical HMD position to eliminate spatial disorientation.
4. **Motion Sickness Mitigation**: Dynamic FOV tunneling vignette (`ComfortVignette`) that smoothly activates during acceleration or turning.
5. **Adaptive Grounding & Surface Collision**: Dual-layer detection (`GroundDetector`) combining `SphereCast` and high-precision fallback `Raycast` to handle rugged terrain without clipping or subterranean camera placement.
6. **Contextual Audio & Controller Haptics**: Footstep sound playback calibrated to actual horizontal distance traveled (`MovementAudio`), paired with subtle controller vibration (`LocomotionHaptics`).
7. **Procedural Wildlife & Biodiversity Simulation**:
   - **Avian Flocking (`BirdFlockSpawner` / `BirdFlockController`)**: Autonomous boids flocking simulation with cohesion, separation, velocity matching, banking, terrain obstacle clearance, and roosting cycles.
   - **Butterfly Dynamics (`ButterflySpawner` / `ProceduralButterflyController`)**: Organic flight paths featuring 3D Perlin turbulence, shrub/flower resting intervals, and dynamic wing fluttering.
   - **Insect & Millipede Crawlers (`InsectLineSpawner` / `InsectLineController`)**: Natural ground crossings with macro longitudinal spine contouring, terrain normal alignment, 3-whisker obstacle avoidance around trees/rocks, and a natural foliage-burrowing lifecycle.
8. **Parabolic Teleportation**: Integrated parabolic arc teleport controller with real-time slope and trail-boundary validation.

---

## 🛠️ System Architecture

The project is structured into modular, decoupled subsystems:

```
[ XR Player Rig ]
  ├── LocomotionManager (Central Coordinator & Origin Setup)
  ├── CharacterController (Physics & Capsule Bounds)
  ├── ContinuousLocomotion (Joystick Translation & Movement Physics)
  ├── PathMovementConstraint (Terrain Splatmap & Physics Trail Boundary Enforcement)
  ├── RotationController (HMD-Centered Snap / Smooth Turning)
  ├── GroundDetector (SphereCast / Raycast Ground Probing)
  ├── LocomotionStateMachine (Locomotion State Machine & Event Dispatcher)
  ├── LocomotionModifier (POI Speed Multipliers)
  ├── ComfortVignette (Dynamic Peripheral Tunneling)
  ├── MovementAudio (Distance-Based Footstep System)
  ├── LocomotionHaptics (Controller Vibration Telemetry)
  ├── TeleportController (Parabolic Arc & Target Marker)
  └── LocomotionDebugOverlay (Real-Time Telemetry HUD)

[ Biodiversity & Wildlife Engine ]
  ├── BiodiversityManager (Orchestrator for Wildlife Density & Proximity Spawning)
  ├── BirdFlockSpawner & BirdFlockController (Autonomous 3D Boids Flocking)
  ├── ButterflySpawner & ProceduralButterflyController (Perlin Turbulence & Landing States)
  └── InsectLineSpawner & InsectLineController (Ground Spine Contouring & Concealment)
```

---

## 📂 Core Script Modules

### Locomotion & Navigation
| Script | Description |
|---|---|
| [`LocomotionManager.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Core/LocomotionManager.cs) | Central hub connecting player sub-components and managing floor-level XR tracking origin setup. |
| [`ContinuousLocomotion.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Movement/ContinuousLocomotion.cs) | Handles continuous translation via `CharacterController.Move()`, radial deadzones, smooth acceleration, slope scaling, and trail constraints. |
| [`PathMovementConstraint.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Movement/PathMovementConstraint.cs) | Enforces trail containment via fast 3D alphamap caching or physics colliders with edge repulsion and slide projection. |
| [`RotationController.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Movement/RotationController.cs) | Controls snap and smooth turning using `transform.RotateAround()` centered on the HMD's physical head position. |
| [`GroundDetector.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Movement/GroundDetector.cs) | Probes environment below the capsule, determining grounding status, slope angle, surface normals, and ground contact points. |
| [`LocomotionStateMachine.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Core/LocomotionStateMachine.cs) | Evaluates and broadcasts locomotion states (`Grounded`, `Walking`, `SteepSlope`, `Airborne`, `Teleporting`). |
| [`ComfortVignette.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Comfort/ComfortVignette.cs) | Procedural peripheral vision vignette fading in during high acceleration and velocity. |
| [`MovementAudio.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/AudioAndHaptics/MovementAudio.cs) | Distance-traveled footstep system adjusting footstep cadence naturally based on physical speed. |
| [`POIZone.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Interaction/POIZone.cs) | Trigger zone that smoothly scales player speed when entering key observation locations. |
| [`TeleportController.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Teleport/TeleportController.cs) | Parabolic arc teleportation controller supporting slope validation and trail boundary checks. |

### Biodiversity & Wildlife
| Script | Description |
|---|---|
| [`BiodiversityManager.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Biodiversity/BiodiversityManager.cs) | Master coordinator managing concurrent active wildlife agents, participant observation ranges, and spawn budgets. |
| [`BirdFlockSpawner.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Biodiversity/BirdFlockSpawner.cs) | Configures flock sizes, flight boundaries, speed envelopes, and waypoint generation. |
| [`BirdFlockController.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Biodiversity/BirdFlockController.cs) | Executes 3D boids flocking rules, aerodynamic banking, obstacle evasion, and altitude maintenance. |
| [`ButterflySpawner.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Biodiversity/ButterflySpawner.cs) | Spawns individual and grouped butterflies near trail flora, shrubs, and meadow waypoints. |
| [`ProceduralButterflyController.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Biodiversity/ProceduralButterflyController.cs) | Simulates organic flutter dynamics, 3D Perlin noise turbulence, landing states, and wing oscillation. |
| [`InsectLineSpawner.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Biodiversity/InsectLineSpawner.cs) | Distance- and timer-triggered trail crossing events for ground crawlers, spawning from edge foliage. |
| [`InsectLineController.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Biodiversity/InsectLineController.cs) | Physics-informed crawler movement with macro spine contouring, 3-whisker obstacle avoidance, and burrowing lifecycle. |
| [`ProceduralInsectAnimator.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Biodiversity/ProceduralInsectAnimator.cs) | Procedural sinusoidal body wave and leg oscillation fallback for unrigged crawler models. |
| [`MillipedeSetupTool.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Editor/MillipedeSetupTool.cs) | Editor automation utility for configuring insect prefabs, mesh orientation offsets, and collider setups. |

---

## 💻 Tech Stack & Requirements

- **Unity Editor**: `6000.5.0f1` (or compatible Unity 6 release)
- **Render Pipeline**: Universal Render Pipeline (URP)
- **XR Runtime**: OpenXR with Meta XR Core SDK
- **Input System**: Unity Input System with OVRInput & Keyboard fallbacks
- **Target Hardware**: Meta Quest 2, Meta Quest 3, Meta Quest Pro, PCVR via Link / AirLink

---

## 🚀 Getting Started

### 1. Repository Setup
```bash
git clone https://github.com/Tessymed/Ecosystem_Choices.git
cd Ecosystem_Choices
```

### 2. Open Project in Unity
1. Launch **Unity Hub**.
2. Click **Add** -> **Add project from disk**.
3. Select the `Ecosystem_Choices` repository root folder.
4. Ensure Unity version `6000.5.0f1` (or latest Unity 6 version) is selected.

### 3. Controls & Playtesting
Open `Assets/Scenes/SampleScene.unity` and enter Play Mode:

| Action | VR Controllers (Meta Quest) | Editor Keyboard / Mouse |
|---|---|---|
| **Continuous Move** | Left Thumbstick | **W / A / S / D** or Arrow Keys |
| **Turn Left / Right** | Right Thumbstick (Snap / Smooth) | **Q / E** |
| **Parabolic Teleport** | Right Thumbstick Press / Forward | Hold **T** |
| **Speed POI Interaction** | Proximity to flora/specimens | Automatic speed dampening |

---

## 📝 Development Roadmap

- [x] Physics-based continuous locomotion with smooth acceleration and radial deadzones.
- [x] HMD-centered snap and smooth turning.
- [x] Dual-stage `SphereCast` / `Raycast` ground detection.
- [x] Automated terrain elevation placement and height snapping.
- [x] Distance-calibrated footstep audio cadence and controller haptics.
- [x] Point of Interest (POI) observation zones and velocity damping.
- [x] Dynamic comfort vignette overlay.
- [x] Terrain-painted splatmap path constraint with smooth edge repulsion.
- [x] Avian 3D boids flocking system with obstacle clearance and 3D spatial flight audio.
- [x] Procedural butterfly flight simulation with horizontal flight orientation, organic wing fluttering, weighted cluster spawning, and plant-to-plant flora meandering.
- [x] Double-sided URP alpha-clip wing materials and multi-hue butterfly color variations.
- [x] Terrain-contouring ground crawler simulation with calm millipede pacing (~0.14 m/s), synchronized leg gait ripple, inspector speed sliders, obstacle feelers, and burrowing lifecycle.
- [x] Immersive ecosystem audio architecture:
  - Velocity-scaled continuous footsteps on forest gravel/grass with acceleration pitch modulation and zero sound on stop.
  - Dynamic organic ambience with multi-frequency breathing LFO flows, wavelength breeze swells, and movement reactivity.
  - 3D spatial tree canopy bird calls and sky flock flyby binaural soundscape.
- [x] Quest Link OpenXR stability optimization (elimination of mobile AR conflicts).
- [ ] Interactive ecological choice cards and specimen data UI.
- [ ] Multi-zone biome transitions (Wetlands, Forest Canopy, Grasslands).
- [ ] Dynamic weather & diurnal day/night cycles.

---

## 📄 License

This project is created for educational research, immersive XR locomotion experimentation, and ecological simulation prototyping.
