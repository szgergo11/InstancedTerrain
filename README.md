# InstancedTerrain - Unity URP

## Brief description
Implementing instanced rendering and real-time culling in Unity. Everything is done on the gpu with compute shaders, rendering is implemented in renderer features with custom passes.

## Project status
Experiment.

## Completed features
- 2 stage frustum culling
	- Firstly grouping objects into cells and frustum testing these (can be optionally visualized with gizmos)
	- Then testing objects individually in the intersected cells
- Full occlusion culling via Hierarchical Z-buffer
- Indirect instanced rendering (using DrawMeshInstancedIndirect)
- Very primitive positioning system

## Work in progress features
- Independent shadow-mesh culling
- Better positioning system
- Shader graph support
- LOD
- Example environment shaders (grass, trees, rocks, etc.)