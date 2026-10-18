# Grassland ecology source

Authored through Blender MCP with `Tools/Art/BuildGrasslandEcology.py`.

`GrasslandEcology.blend` is a dedicated library scene; the user's active scene is preserved. The version 1 JSON contains two templates, `Butterfly` (136 triangles) and `Firefly` (96 triangles). Positions and normals already use Unity Y-up coordinates, triangles reverse the Blender-to-Unity handedness, vertex colors remain authored sRGB, and `wings` marks deforming butterfly vertices. Unity bakes the templates into regional native meshes; no source files are read at runtime.

The builder refuses to replace existing exports. Retain the source and review changes explicitly when revising models. Runtime ownership, baking, animation bounds and validation are documented in `Docs/GrasslandEcology.md`.
