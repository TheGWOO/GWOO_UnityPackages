# Rendering Debugger Extensions

Unity Rendering Debugger additions for validating scene asset display quality.

## What It Does

- Adds an `Asset Validity` panel to the Rendering Debugger.
- Scans visible `MeshRenderer` and `SpriteRenderer` objects from a selected reference camera.
- Reports texture texel density as source texels divided by displayed screen pixels.
- Draws scene/game view boxes, labels, and a density legend for visible results.

## Usage

Open the Rendering Debugger and select `Asset Validity > Texel Density`.

1. Pick a reference camera.
2. Choose renderer types and severity filters.
3. Set the target texel-per-pixel ratio and tolerances.
4. Click `Rescan Scene` to populate the result table.
5. Enable boxes, labels, or legend overlays while inspecting results.

## Measurement Notes

- Displayed size is estimated from the projected screen-space bounds of each renderer.
- Off-screen projection is clamped to the camera pixel area, so partial visibility reflects displayed pixels.
- Mesh renderers use the main texture on the shared material.
- Sprite renderers use the sprite rect dimensions.

## Known Limits

- Mesh UV layout, submeshes, material tiling, and non-main textures are not fully accounted for yet.
- Bounds projection is more accurate than center-depth estimation, but it is still a bounds-based approximation rather than per-triangle coverage.
- The package currently covers development builds through its asmdef define constraints.
