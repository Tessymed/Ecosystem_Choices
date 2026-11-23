# Ecosystem Choices - XR Locomotion, Biodiversity & Ecological Simulation

[![Unity 6](https://img.shields.io/badge/Unity-6000.6.10f1-blue.svg)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/URP-Universal%20Render%20Pipeline-brightgreen.svg)](https://unity.com/srp/Universal-Render-Pipeline)
[![Target Platform](https://img.shields.io/badge/Target-Meta%20Quest%203%20%7C%20OpenXR-0082FB.svg)](https://www.meta.com/quest/quest-3/)
[![License](https://img.shields.io/badge/License-MIT-lightgrey.svg)](LICENSE)

An immersive Virtual Reality (VR) experience designed for **Meta Quest 3**, **Meta Quest 2**, and **OpenXR** headsets. **Ecosystem Choices** merges comfort-driven VR locomotion and path constraint systems with dynamic, procedural biodiversity simulations (avian flocking, butterfly flight mechanics, and ground insect trail crossings), interactive VR menus, dynamic flowing river systems, and multi-surface locomotion audio crossfading.

---

## 📌 Project Overview & Scope

The prototype delivers a fully realized, comfort-focused VR ecosystem exploration framework:

- **Interactive VR Startup Void & In-Game UI**: Seamless UI card presentation in a pure black void on startup, smooth cinematic fade-in with zero camera drop, and in-game pause dialog.
- **Physics-Informed VR Locomotion**: Continuous joystick translation, radial deadzones, slope speed adaptation, and HMD-centered snap/smooth turning.
- **Intelligent Trail & Path Constraints**: High-performance painted terrain splatmap sampling (`PathMovementConstraint`) preventing off-trail wandering while allowing natural side bleed and smooth edge sliding.
- **Flowing River & Dynamic 3D Spatial Audio**: Canal trench cutting through the low-poly terrain with animated flowing water and a dynamic 3D audio emitter that tracks the closest point on the canal to the player.
- **Wooden Bridge & Multi-Surface Footstep Crossfading**: Equal-power crossfading ($\cos^2 + \sin^2 = 1$) between gravel/dirt trail footsteps and wooden bridge plank footsteps, with zero volume drop or audio pops.
- **Autonomous Biodiversity Engine**:
  - **Avian Flocking**: 3D boids simulation with banking, obstacle evasion, and roosting cycles.
  - **Procedural Butterflies**: 3D Perlin flight turbulence, double-sided wing fluttering, and flower-resting states.
  - **Ground Crawlers / Millipedes**: Ground spine contouring, 3-whisker obstacle avoidance, and burrowing lifecycle.
  - **Tree Canopy Birds**: Randomized 3D spatial bird calls originating from surrounding foliage.

---

## 🛠️ System Architecture

```
[ XR Player Rig (OVRCameraRig) ]
  ├── LocomotionManager (Central Coordinator, XR Floor Origin & Terrain/Bridge Sync)
  ├── CharacterController (Physics & Capsule Bounds)
  ├── ContinuousLocomotion (Smooth Translation, Acceleration & Movement Physics)
  ├── PathMovementConstraint (Terrain Splatmap & Wooden Bridge Boundary Enforcement)
  ├── RotationController (HMD-Centered Snap / Smooth Turning)
  ├── GroundDetector (SphereCast / Raycast Ground Probing)
  ├── LocomotionStateMachine (State Management: Grounded, Walking, Airborne, etc.)
  ├── ComfortVignette (Dynamic Peripheral Tunneling)
  ├── MovementAudio (Multi-Surface Footstep Audio & Equal-Power Crossfader)
  ├── TeleportController (Parabolic Arc & Target Marker)
  └── LocomotionDebugOverlay (Real-Time Telemetry HUD & Surface Blend Meter)

[ VR UI & Interaction ]
  ├── VRMenuManager (Void Culling, Home/About/Quit Cards & Cinematic Fade-In)
  ├── VRRaycaster (3D Controller Laser Pointer & UI Event System)
  └── VRMenuButtonEffect (Smooth Button Hover/Depression, Audio & Haptics)

[ Environmental Systems ]
  ├── River_Water (Flowing River Mesh & URP Material)
  ├── RiverSoundController (Centerline Spline Tracking & 3D Spatial Audio Attenuation)
  ├── PT_Wooden_Bridge_02 (Wooden Canal Bridge with Mesh Colliders)
  └── WoodenBridgeSurface (Surface Registration, Bounding Bounds & Path Layer Assignment)

[ Biodiversity Simulation ]
  ├── BirdFlockSpawner & BirdFlockController (Autonomous 3D Boids Flocking)
  ├── ButterflySpawner & ProceduralButterflyController (Perlin Turbulence & Landing States)
  └── InsectLineSpawner & InsectLineController (Ground Spine Contouring & Burrowing Lifecycle)
```

---

## 🎮 Core Subsystems & Features

### 1. VR UI Menu System & Startup Void
- **Total Pitch-Black Startup Void**: At launch, the camera's culling mask hides the 3D world entirely (`ApplyVoidCulling`), presenting only the high-resolution UI cards directly in front of the player's headset against deep black space.
- **Interactive UI Cards**:
  - **Home Card**: Experience branding with **Start**, **About**, and **Quit** buttons.
  - **About Card**: Narrative and ecological context with a **Back** button.
  - **Quit Card**: Confirmation modal with **Yes** (exit) and **No** (resume/return) buttons.
- **3D Laser Pointer & Haptics**: Right VR controller projects a responsive 3D raycast laser with glowing reticle, haptic feedback pulses on hover/click, and crisp audio cues.
- **Smooth Cinematic Fade-In**: Clicking **Start** initiates a smooth 1.8-second black fade-in during which the world ambient soundscape and flowing river audio gently swell into the user's ears.
- **Zero Player Drop**: The player is pre-spawned firmly on the walking trail $(13.6, 4.74, 36.84)$, preventing any disorienting downward falling.
- **Mid-Experience Pause Menu**: Pressing the VR Menu button (or `B` / `Y` / `Esc`) opens the Quit/Resume dialog mid-experience.

### 2. Multi-Surface Locomotion Audio & Equal-Power Crossfade
- **Dual-Surface Acoustic Tracks**:
  - *Track A (Terrain Trail)*: Continuous walking audio on dirt/gravel (`Footsteps_Gravel_Grass_Continuous.mp3`).
  - *Track B (Wooden Bridge)*: Continuous walking audio on resonant wooden planks (`footsteps-on-wood-397989.mp3`).
- **Equal-Power Crossfade**:
  $$\text{terrainWeight} = \cos\left(\text{blend} \cdot \frac{\pi}{2}\right), \quad \text{woodWeight} = \sin\left(\text{blend} \cdot \frac{\pi}{2}\right)$$
  Maintains constant acoustic energy ($\cos^2 + \sin^2 = 1$) throughout the transition, eliminating volume dips.
- **Multi-Method Bridge Detection (`WoodenBridgeSurface.cs`)**:
  - Downward ground hit inspection (`GroundDetector.SurfaceHit`).
  - Spatial 3D bounding volume with $0.8\,\text{m}$ lead-in padding for smooth ramp entry.
  - Localized feet overlap sphere checking.
- **Speed & Cadence Scaling**: Footstep playback pitch ($0.88\times$ to $1.22\times$) scales dynamically with player speed, fading to instant silence when stationary.

### 3. Flowing River & Spatial 3D Audio
- **Canal Trench**: Dug river ridge traversing across the terrain along $Z = 51.1\,\text{m}$.
- **Optimized Dynamic Spatial Audio (`RiverSoundController.cs`)**:
  - Avoids spawning multiple expensive 3D audio sources.
  - Projects a single 3D audio emitter along the canal centerline to the point nearest to the player's head in real-time.
  - Features realistic logarithmic distance roll-off ($3\,\text{m}$ to $25\,\text{m}$) and distance-based volume culling ($>35\,\text{m}$).

### 4. Trail & Path Movement Constraints
- **Painted Terrain Alphamap Sampling (`PathMovementConstraint.cs`)**:
  - Pre-loads splatmap arrays into memory for 0-GC per-frame evaluation.
  - Constrains the player to the designated trail with configurable bleed distance ($1.2\,\text{m}$) and smooth edge repulsion.
- **Bridge Walkway Pass-Through**: Automatically recognizes `WoodenBridgeSurface` as a valid path, ensuring uninterrupted crossing over the river canal.

---

## 🕹️ Controls & Interaction Guide

| Action | Meta Quest 3 Controllers | Editor Keyboard / Mouse |
|---|---|---|
| **Interact with UI** | Right Trigger (Index) | Left Mouse Click |
| **Aim UI Laser** | Right Hand Pointer | Mouse Cursor |
| **Continuous Locomotion** | Left Thumbstick | **W / A / S / D** or Arrow Keys |
| **Turn Left / Right** | Right Thumbstick (Snap / Smooth) | **Q / E** |
| **Toggle Pause / Quit Menu** | Left Menu Button / **B** / **Y** Button | **Escape** key |
| **Parabolic Teleport** | Right Thumbstick Press / Forward | Hold **T** |

---

## 📦 Meta Quest 3 Build & Deployment Guide

Follow these steps to produce an optimized standalone APK for **Meta Quest 3**:

### Step 1: Switch Platform to Android
1. In Unity Editor, go to **File** > **Build Profiles** (or **Build Settings**).
2. Under **Platform**, select **Android**.
3. Set **Texture Compression** to **ASTC**.
4. Click **Switch Platform** and allow Unity to re-import assets for Android.

### Step 2: Verify XR Plug-in Management
1. Open **Edit** > **Project Settings** > **XR Plug-in Management**.
2. Switch to the **Android** tab (robot icon).
3. Ensure **OpenXR** is checked.
4. Under the **OpenXR** sub-tree:
   - Ensure the **Meta Quest Support** feature group is enabled.
   - In **Interaction Profiles**, ensure **Oculus Touch Controller Profile** and **Meta Quest Touch Plus Controller Profile** are added.
5. In **Project Settings** > **Player** > **Android tab**:
   - **Color Space**: Linear.
   - **Graphics API**: Vulkan (or OpenGLES3 fallback).
   - **Minimum API Level**: Android 10.0 (API Level 29) or Android 12.0 (API Level 32).
   - **Target API Level**: Automatic (highest installed) or API Level 34.
   - **Scripting Backend**: IL2CPP.
   - **Target Architectures**: ARM64 checked (ARMv7 unchecked).

### Step 3: Build Standalone APK
1. In **File** > **Build Settings**, verify `Assets/Scenes/SampleScene.unity` is checked in **Scenes In Build**.
2. Click **Build** (or **Build and Run** if your Quest 3 is connected via USB-C with Developer Mode enabled).
3. Choose a destination folder and filename, e.g. `Builds/Ecosystem_Choices_Quest3.apk`.

### Step 4: Install to Meta Quest 3
- **Option A (Meta Quest Developer Hub / MQDH)**: Drag and drop the built `.apk` directly into MQDH under **Device Manager** > **Apps**.
- **Option B (SideQuest)**: Connect Quest 3 via USB-C, click the **Install APK** button in the top menu bar, and select the `.apk`.
- **Option C (Command Line / ADB)**:
  ```bash
  adb install -r Builds/Ecosystem_Choices_Quest3.apk
  ```
- **Launching on Quest 3**: Put on the headset, navigate to **App Library**, change the filter in the top right to **Unknown Sources**, and select **Ecosystem Choices**.

---

## 📂 Core Scripts Directory

| File | Purpose |
|---|---|
| [`VRMenuManager.cs`](Assets/Scripts/UI/VRMenuManager.cs) | Controls startup void culling, menu page switching, cinematic fade-in, and pause menu. |
| [`VRRaycaster.cs`](Assets/Scripts/UI/VRRaycaster.cs) | 3D VR laser pointer with curved reticle and UI canvas raycasting. |
| [`VRMenuButtonEffect.cs`](Assets/Scripts/UI/VRMenuButtonEffect.cs) | Haptic pulses and visual depression animations on button hover/click. |
| [`MovementAudio.cs`](Assets/Scripts/Locomotion/AudioAndHaptics/MovementAudio.cs) | Distance-based footstep playback, dynamic pitch scaling, and equal-power multi-surface crossfade. |
| [`WoodenBridgeSurface.cs`](Assets/Scripts/Environment/WoodenBridgeSurface.cs) | Identifies wooden bridge structures, spatial bounds, and assigns path layers. |
| [`RiverSoundController.cs`](Assets/Scripts/Environment/RiverSoundController.cs) | 3D spatial river audio tracking the nearest point along the canal centerline. |
| [`PathMovementConstraint.cs`](Assets/Scripts/Locomotion/Movement/PathMovementConstraint.cs) | Splatmap terrain trail restriction with slide projection and bridge support. |
| [`ContinuousLocomotion.cs`](Assets/Scripts/Locomotion/Movement/ContinuousLocomotion.cs) | Physics-driven translation, acceleration, deceleration, and slope scaling. |
| [`GroundDetector.cs`](Assets/Scripts/Locomotion/Movement/GroundDetector.cs) | Dual SphereCast/Raycast ground probing for accurate terrain alignment. |
| [`RotationController.cs`](Assets/Scripts/Locomotion/Movement/RotationController.cs) | HMD-centered snap and smooth turning. |
| [`ComfortVignette.cs`](Assets/Scripts/Locomotion/Comfort/ComfortVignette.cs) | Dynamic FOV tunneling during rapid acceleration and turning. |
| [`LocomotionDebugOverlay.cs`](Assets/Scripts/Locomotion/Debug/LocomotionDebugOverlay.cs) | On-screen HUD showing locomotion telemetry and real-time surface blend percentage. |
| [`BirdFlockSpawner.cs`](Assets/Scripts/Biodiversity/BirdFlockSpawner.cs) | Avian 3D boids flocking spawner and altitude controller. |
| [`ButterflySpawner.cs`](Assets/Scripts/Biodiversity/ButterflySpawner.cs) | Procedural butterfly spawner with organic flight turbulence. |
| [`InsectLineSpawner.cs`](Assets/Scripts/Biodiversity/InsectLineSpawner.cs) | Ground insect crossing spawner with spine contouring. |

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
