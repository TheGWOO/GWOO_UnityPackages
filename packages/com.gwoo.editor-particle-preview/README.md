# Editor Particle Preview

Deterministic edit-mode ParticleSystem playback for custom Unity editor tools.

## What It Does

- Registers particle systems into preview sessions.
- Supports deterministic seeds and fixed timestep simulation.
- Exposes seek and advance APIs for timeline-style editor windows.
- Clears preview-only particle state before assets are saved.

## Usage

Use `EditorParticleSystemDriver` from the `GWOO.Editor.ParticlePreview` namespace when building custom animation or FX preview windows that need deterministic particle scrubbing in Edit Mode.

