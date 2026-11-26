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

## 📦 Meta Quest 3 Build & Playtester Installation Guide

There are two primary ways for external playtesters to install and experience **Ecosystem Choices**:

### Method 1: Meta App Lab Shareable Link (Recommended - Zero Cable / Zero Dev Mode)
If you have uploaded the build to Meta App Lab (Alpha, Beta, or App Lab Store):
1. **No Developer Mode or PC required** for your playtesters.
2. The developer provides a **Public App Lab URL** or a **Release Channel Invite Link** (generated from the [Meta Quest Developer Dashboard](https://developer.oculus.com/manage/) under **Distribution** > **Release Channels** > **Alpha** > **Invite Links**).
3. The playtester opens the link on their phone/browser, clicks **Accept / Get**, and the app automatically downloads into their Meta Quest 3 headset with full support for future automatic updates.

---

### Method 2: Sideloading the Standalone APK (SideQuest / MQDH / ADB)
For playtesters downloading the compiled `.apk` directly from GitHub Releases:

#### A. Requirements:
- A Meta Quest 3 or Quest 2 headset.
- A USB-C cable (or Wi-Fi if wireless ADB is configured).
- A free [SideQuest](https://sidequestvr.com/) account (Desktop or Web installer).

#### B. Installation Steps via SideQuest:
1. Download the latest `Ecosystem_Choices_vXX.apk` from the **[Releases](../../releases)** section of this repository.
2. Open **SideQuest** on your PC/Mac (or navigate to https://sdq.st/download on a WebUSB-compatible browser like Chrome/Edge).
3. Connect your Quest 3 to your computer via USB-C and put on the headset to click **"Allow USB Debugging"**.
4. In SideQuest, click the **"Install APK file from folder on computer"** icon (an icon of a folder with a downward arrow in the top right header).
5. Select the downloaded `.apk` file. SideQuest will install the package.
6. **Launching the Game**:
   - Put on your Quest 3.
   - Open your **App Library** (grid icon in the universal menu).
   - In the search/filter dropdown in the upper right (which defaults to "All"), select **Unknown Sources**.
   - Click **Ecosystem Choices** to launch the experience.

#### C. Installation Steps via Command Line (ADB):
```bash
adb install -r Ecosystem_Choices_v04.apk
```

---

## 🛠️ Building the Standalone APK from Source

Follow these steps in Unity to produce an optimized standalone APK for **Meta Quest 3**:

### Step 1: Switch Platform to Android
1. In Unity Editor, go to **File** > **Build Profiles** (or **Build Settings**).
2. Under **Platform**, select **Android (Meta Quest)**.
3. Set **Texture Compression** to **ASTC**.
4. Click **Switch Platform**.

### Step 2: Graphics & Quest 3 Runtime Optimizations
This project includes pre-configured pipeline optimizations in `Mobile_RPAsset.asset`:
- **URP Depth & Opaque Textures**: Enabled with $2\times$ downsampling for realistic water refractions, shoreline foam, and depth gradients.
- **HDR & Native Render Scale**: Enabled with $1.0\times$ native resolution and soft shadow filtering for vibrant, high-fidelity lighting.
- **72 Hz Physics Lockstep**: `Time.fixedDeltaTime = 0.013888889` (72 Hz) and V-Sync enabled to eliminate micro-stuttering.
- **Anti-Jitter Capsule Filter**: 6 cm deadband height adjustment and 8 cm step offset preventing ground penetration vibration over low-poly terrain.
- **Trail Sliding Constraint**: Smooth tangent sliding along trail edges instead of hard boundary snapping.

### Step 3: Build Standalone APK
1. In **File** > **Build Settings**, verify `Assets/Scenes/SampleScene.unity` is checked.
2. Click **Build** and output the APK (e.g. `Builds/Ecosystem_Choices_v04.apk`).

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
