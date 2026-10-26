---
name: hz-meta-xr-operator-cubeshooter-scene
description: "The CubeShooter sample scene (a.k.a. Vaporwave Shooting Gallery) for Meta XR Operator — grab a laser gun and shoot floating target cubes with controllers or hands. What the scene contains and the few non-obvious bits for driving it; discover the rest via XR Operator. Read before testing or demoing this scene."
allowed-tools:
  - Bash(metavr:*)
  - Bash(hzdb:*)
---

# CubeShooter Sample Scene

## Goal

A neon shooting gallery that demonstrates Meta XR Operator driving a full gameplay loop — **grab the gun, aim, fire, score** — with either controllers or hands.

## Scene at a glance

- **`LaserGun`** (root) — grabbable pistol with a **`MuzzlePoint`** child (the fire point; the barrel points along its local +Z).
- **`Target_*`** cubes — the `TargetSpawner` keeps ~16 floating around the player and respawns one whenever a cube is destroyed. Points are gained upon hitting a cube.
- **`ScoreManager` / `ScoreDisplay`** — the running score.
- Tracking origin is **EyeLevel → OpenXR `local`** (confirm via `unity_get_world_pose`).

Discover everything else yourself with `get_scene_root_objects` / `get_children` / `get_world_pose` + captures — see **hz-meta-xr-operator**.

## Interacting

The gun is a **custom proximity grab** (not an ISDK interactable): it latches when a controller/hand comes within the `grabRadius` (**~0.3 m**, serialized on the `LaserGun` component) of `LaserGun` and reparents to that anchor, then tracks it. So:

- **Grab** — put the controller grip (or hand wrist) on the gun and press **grip** (controller) or make a **fist/curl** (hand). The gun's root path disappearing (reparented under the anchor) confirms the grab. Release with grip-up / opening the hand.
- **Aim + fire** — aim with **hz-meta-xr-operator-grabbed-objects** (calibrate the `MuzzlePoint` offset once, then apply `Q_aim`). The gun **reparents to the anchor and tracks the controller _grip_ pose (or the hand _wrist_)** — so drive the **grip** pose (`set_controller_pose(grip,…)`) / `openxr_set_hand_pose(wrist_orientation=…)`, **not** the aim pose. Fire with the **index Trigger** (controller) or an **index pinch** (hand); a ~0.3–0.4 s hold looses a couple rounds (`fireRate`≈0.15 s). Verify a hit via the on-screen `Score` rising or the target's `unity_get_world_pose` returning *not found*. Note: an `openxr_hand_gesture(pinch, auto_release=true)` opens the hand and **drops the gun** — re-grab for another shot (or hold a sticky pinch). To re-fire without dropping, cycle `grab`→`pinch` (grab keeps the hold, pinch produces a fresh fire edge).
- **Switching controllers ⇄ hands** — the scene supports both, but the gun's grab gives **controllers priority**: a hand grab is only attempted when *neither* controller is in-hand. So to switch from controllers to hands, inject synthetic hands on **both** sides — a single leftover simulated controller keeps the gun in controller mode and the hand grab silently no-ops. No need to release inputs first. See **hz-meta-xr-operator-hand-tracking**.
- Coordinate conversion — see **hz-meta-xr-operator-coordinates**.

## CubeShooter-Broken

A sibling scene with intentional bugs (broken "alt" prefabs/scripts) for exercising an agent's ability to find and fix issues — pair with Unity MCP.

## Setup

These interactions need Meta XR Operator running (the `openxr_*` MCP tools). If they're unavailable: install the **Meta XR Core SDK** + **Meta XR Simulator**, complete setup via the **Meta Project Setup Tool** / **Meta XR Settings** window, and check status in the Unity toolbar under **Meta**. The full agent skill set (**hz-meta-xr-operator** and friends) ships in the Core SDK at `Editor/MetaXROperator/Skills`.
