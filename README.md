# Ecosystem Choices - XR Locomotion & Ecological Simulation Prototype

[![Unity 6](https://img.shields.io/badge/Unity-6000.5.0f1-blue.svg)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/URP-Universal%20Render%20Pipeline-brightgreen.svg)](https://unity.com/srp/Universal-Render-Pipeline)
[![Platform](https://img.shields.io/badge/Target-Meta%20Quest%20%7C%20OpenXR-0082FB.svg)](https://www.meta.com/quest/)
[![License](https://img.shields.io/badge/License-MIT-lightgrey.svg)](LICENSE)

An immersive Virtual Reality (VR) prototype designed for Meta Quest and OpenXR headsets. **Ecosystem Choices** merges physics-driven VR locomotion with ecological observation mechanics, allowing users to explore natural environments, observe organism dynamics, and make choices affecting local ecosystems.

---

## 📌 Project Overview & Scope

The goal of this prototype is to establish a **comfort-focused, highly responsive VR locomotion framework** combined with **interactive environmental Point of Interest (POI) observation zones**.

### Key Objectives
1. **Comfortable Continuous Locomotion**: Provide smooth thumbstick-driven translation with customizable radial deadzones, dynamic speed scaling on inclines, and automated terrain ground-alignment.
2. **Head-Centered Rotation**: Offer both **Snap Turn** (angle increments: 15°, 30°, 45°, 60°) and **Smooth Turn** (60°–90°/s) pivoting around the user's HMD position to eliminate spatial displacement.
3. **Motion Sickness Mitigation**: Dynamic FOV tunneling vignette (`ComfortVignette`) that smoothly activates during movement acceleration.
4. **Adaptive Grounding & Surface Collision**: Dual-layer detection (`GroundDetector`) combining `SphereCast` and high-precision fallback `Raycast` to handle uneven terrain without camera ground clipping or subterranean spawning.
5. **Contextual Audio & Controller Haptics**: Footstep sound playback calibrated to actual horizontal distance traveled rather than fixed timers (`MovementAudio`), paired with subtle controller vibration (`LocomotionHaptics`).
6. **POI Observation Zones**: Point of Interest trigger zones (`POIZone` & `LocomotionModifier`) that smoothly reduce exploration speed when approaching flora/fauna specimens, encouraging deliberate observation.
7. **Parabolic Teleportation**: Alternate parabolic arc teleport system with slope validation and landing markers for users preferring instant teleportation.

---

## 🛠️ Architecture & System Design

The architecture is built around modular, decoupled components attached to the main XR Player root:

```
[ XR Player Root ]
  ├── LocomotionManager (Main Coordinator)
  ├── CharacterController (Physics & Capsule Bounds)
  ├── ContinuousLocomotion (Joystick Translation & Ground Snapping)
  ├── RotationController (HMD-Centered Snap/Smooth Turning)
  ├── GroundDetector (SphereCast/Raycast Terrain Probing)
  ├── LocomotionStateMachine (State Evaluation & Event System)
  ├── LocomotionModifier (POI Speed Multipliers)
  ├── ComfortVignette (Dynamic Peripheral Tunneling)
  ├── MovementAudio (Distance-Based Footstep System)
  ├── LocomotionHaptics (Controller Vibration Telemetry)
  ├── TeleportController (Parabolic Arc & Target Marker)
  └── LocomotionDebugOverlay (Real-Time Telemetry HUD)
```

### Core Script Modules

| Script | Responsibility |
|---|---|
| [`LocomotionManager.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Core/LocomotionManager.cs) | Central hub connecting player sub-components and managing floor-level XR tracking origin setup. |
| [`ContinuousLocomotion.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Movement/ContinuousLocomotion.cs) | Handles continuous translation via `CharacterController.Move()`, radial deadzone filtering, smooth acceleration/deceleration, slope speed reduction, and vertical ground snapping. |
| [`RotationController.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Movement/RotationController.cs) | Controls snap and smooth turning using `transform.RotateAround()` centered on HMD head position. |
| [`GroundDetector.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Movement/GroundDetector.cs) | Probes environment below the capsule, determining grounding status, slope angle, surface normals, and exact ground surface points. |
| [`LocomotionStateMachine.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Core/LocomotionStateMachine.cs) | Evaluates and broadcasts locomotion states (`Grounded`, `Walking`, `SteepSlope`, `Airborne`, `Teleporting`). |
| [`ComfortVignette.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Comfort/ComfortVignette.cs) | Procedural peripheral vision vignette fading in during high acceleration and velocity. |
| [`MovementAudio.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/AudioAndHaptics/MovementAudio.cs) | Distance-traveled footstep system adjusting footstep cadence naturally based on physical speed. |
| [`POIZone.cs`](file:///d:/lawal/Documents/Unity%20progress/XRCC/Ecosystem_Choices/Assets/Scripts/Locomotion/Interaction/POIZone.cs) | Trigger zone that smoothly scales player speed when entering key observation locations. |

---

## 💻 Tech Stack & Requirements

- **Unity Editor**: `6000.5.0f1` (or compatible Unity 6 release)
- **Render Pipeline**: Universal Render Pipeline (URP)
- **XR Plugin**: OpenXR / Meta XR Core SDK
- **Input System**: Unity Input System + OVRInput fallbacks
- **Target Hardware**: Meta Quest 2, Meta Quest 3, Meta Quest Pro, PCVR via Link/AirLink

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
3. Select the `Ecosystem_Choices` folder.
4. Ensure Unity version `6000.5.0f1` (or latest Unity 6 version) is selected.

### 3. Playtesting in Editor
1. Open `Assets/Scenes/SampleScene.unity`.
2. Connect your Meta Quest via Quest Link / AirLink, or use Keyboard WASD / mouse in Editor for quick testing:
   - **W / A / S / D**: Continuous movement
   - **Q / E**: Snap turn left / right
   - **T**: Hold to aim parabolic teleport arc

---

## 📝 Development Roadmap

- [x] Physics-based continuous locomotion with smooth acceleration.
- [x] HMD-centered snap and smooth turning.
- [x] Dual-stage `SphereCast` / `Raycast` ground detection.
- [x] Auto-snapping terrain height placement (eliminating underground camera spawning).
- [x] Footstep audio cadence synchronized to distance traveled.
- [x] POI observation zone speed dampening.
- [x] Dynamic comfort vignette overlay.
- [ ] Interactive ecological choices & specimen data cards.
- [ ] Multi-zone biome transitions (Wetlands, Forest Canopy, Grasslands).
- [ ] Dynamic weather & organism behavior triggers.

---

## 📄 License

This project is created for educational research and ecological simulation prototyping.
