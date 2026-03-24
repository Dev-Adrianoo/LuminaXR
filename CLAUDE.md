# CLAUDE.md — LuminaXR

## Project
Spatial 3D modeler for Meta Quest 3 (MR/VR). Hand tracking only. No controllers.
Android build · Editor via VDXR

## Goal
The project explores spatial interaction and mesh manipulation
using hand tracking in mixed reality.

## Environment
Unity 6.3 LTS (6000.3.10f1)
URP 17.x
com.unity.xr.hands 1.7.3
Meta XR SDK (check Packages/manifest.json for exact version)

## Stack
- Meta XR SDK (OVRManager, Camera Rig)
- com.unity.xr.hands 1.7.3
- OpenXR Plugin + HandVisualizer sample

## ⚠️ Critical
Never use OVRSkeleton. Use XRHandSubsystem only.
OVRSkeleton requires Meta proprietary extensions — incompatible with VDXR.

## Build
Windows = Editor/VDXR · Android = Quest build · Same code, no changes.

## Scripts
MagneticSnapping.cs — pinch simultâneo (2 mãos), snap, dampening, color feedback, integra VertexHUD
VertexMarker.cs — spawns spheres at cube corners runtime (markerScale 0.05)
MeshInspector.cs — read-only mesh logger (diagnostic)
MeshDeformer.cs — vertexMap int[][], deforma mesh real acompanhando spheres
ObjectGrab.cs — fist detection ambas as mãos, dynamic role assignment, preview mode
VertexHUD.cs — texto flutuante (TMP 3D) mostrando distância durante arrasto

## Conventions
- One responsibility per script
- No empty Update()
- MaterialPropertyBlock only — never material.color
- Cache Shader.PropertyToID as static readonly int
- Comments: Portuguese · Code: English

## Setup
Layer: VertexTarget → all interactable spheres
OpenXR (Windows): Hand Interaction Poses, Meta Quest Support,
Hand Tracking Subsystem, Meta XR Foveation, Palm Pose

## Roadmap
✅ Pinch & Snap · Dampening · Visual Feedback · Vertex Markers
✅ Mesh API → MeshDeformer conectado às esferas, deformação funcionando no Quest
✅ Rotação com a mão
✅ Fechar mão → objeto gruda · Fechar de novo → modo preview (flutua e gira automaticamente)
🔲 Rotação com 2 dedos girando no ar → rotação intencional para modelar
✅ Suporte a duas mãos → pinch simultâneo, dynamic role assignment, pausa preview ao modelar
🔲 Polimento geral: thresholds, feedback visual, estabilidade
✅ Value HUD / Transform Overlay → texto flutuante mostrando distância ao mover vértice (VertexHUD.cs)

## AI Assistant Guidelines
This developer is LEARNING. Prioritize teaching over solving.

- Explain the concept before writing any code
- Guide step-by-step — don't dump full solutions
- Explain WHY, not just what
- Point out new concepts explicitly ("this is called X")
- Ask before implementing anything non-trivial
- Validate understanding before moving forward
- If the developer writes code with bugs, ask them to find it first

## Response Style
- Prefer short explanations first
- Provide code only when necessary
- If code is generated, keep it minimal
- Avoid generating large systems in a single response

## Context Policy
- Never analyze the entire project upfront
- Request only specific files when needed
- Prefer small focused snippets over large files

## Obsidian (Cérebro Externo)
Caminho: `C:\Users\Adria\Documents\Documentation\Dev-Logs\LuminaXR_Dev Log\`

- **Dev Log.md** → roadmap visual com status de cada feature
- **YYYY-MM-DD_Sessao.md** → log detalhado de cada sessão (bugs, fixes, aprendizados)
- **Conceitos.md** → conceitos técnicos aprendidos

Usar como:
- Fonte de contexto: consultar logs anteriores antes de retomar trabalho
- Histórico de bugs: procurar erros e soluções que já aconteceram
- Documentação: ao final de cada sessão, criar/atualizar o log do dia
- Aprendizados: registrar lições técnicas (ex: serialização de prefabs sobrescreve código)