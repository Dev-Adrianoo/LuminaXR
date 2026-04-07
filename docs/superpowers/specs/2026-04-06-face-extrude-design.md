# Face Extrude — Design Spec

> **Data:** 2026-04-06
> **Status:** Aprovado

---

## Problema

O sistema atual so move vertices existentes. Nao ha como criar geometria nova.
Extrude transforma o modelador de "editor de vertices" em "criador de formas".

## Escopo

- Extrude de face inteira (4 vertices / quad)
- Direcao: normal da face (fixa durante o arrasto)
- Feedback: highlight nas 4 esferas + VertexHUD com profundidade
- Reducao do magneticRadius das esferas (0.05 → 0.025)
- Expansivel pra aresta/vertice no futuro (arquitetura preparada, nao implementada)

## Arquitetura

### Novos scripts

**FaceSelector.cs**
- Responsabilidade: detectar qual face esta sob a mao, dar feedback visual
- No `Start()`: constroi lista de `FaceData` a partir de `mesh.triangles`, agrupando triangulos coplanares em quads
- Cada frame: calcula distancia da mao ao centroide de cada face vs distancia ao vertice mais proximo
- Se `dist(centroide) < dist(vertice)` → modo face ativo
- Feedback: 4 esferas da face mudam de cor (amarelo) via MaterialPropertyBlock
- Interface publica:
  - `IsActiveForHand(bool isLeft)` → bool
  - `GetSelectedFace(bool isLeft)` → FaceData (indices + normal + centroid)
  - `GetFaceCentroid(bool isLeft)` → Vector3

**FaceExtrude.cs**
- Responsabilidade: executar a operacao de extrude quando pinch ativa sobre face selecionada
- Ativacao: consulta `FaceSelector.GetSelectedFace()`, pinch ativa → modo extrude
- Operacao de mesh (uma vez no pinch):
  1. Duplica os 4 vertices da face selecionada
  2. Reatribui triangulos da face original pros novos vertices (tampa sobe)
  3. Cria 8 novos triangulos (4 faces laterais × 2 tris cada) conectando originais aos novos
  4. Atualiza `mesh.vertices`, `mesh.triangles`, `mesh.RecalculateNormals()`
- Movimento durante arrasto:
  - `float depth = Vector3.Dot(handDelta, faceNormal)`
  - Move 4 novos vertices: `originalPos + faceNormal * depth`
  - Permite extrude (pra fora) e intrude (pra dentro)
- Spawn de esferas: instancia 4 novas esferas como filhas do objeto
- VertexHUD: mostra distancia (depth) numa das esferas novas
- Finalizacao (release): chama `MeshDeformer.RebuildVertexMap()`
- Registra nova face no FaceSelector → permite extrude em cima de extrude

### Estrutura de dados

```csharp
struct FaceData {
    int[] vertexIndices;     // 4 indices no array de esferas
    int[] triangleIndices;   // 2 triangulos (6 indices no mesh.triangles)
    Vector3 normal;          // calculada em runtime
    Vector3 centroid;        // media das 4 posicoes
}
```

FaceSelector constroi a lista no Start() e a expande quando FaceExtrude cria novas faces.

### Modificacoes em scripts existentes

**HandModeManager.cs**
- Novo enum: `HandMode.Extrude`
- Referencia ao `FaceSelector` no Inspector
- Prioridade: Extrude acima de Modeling, abaixo de Grab

**MagneticSnapping.cs**
- `magneticRadius`: 0.05 → 0.025
- Check existente de HandModeManager: adicionar `HandMode.Extrude` ao block

**MeshDeformer.cs**
- Novo metodo publico: `RebuildVertexMap()` — reconstroi `spheres[]` e `vertexMap[][]` a partir dos filhos atuais
- Update() sem mudancas

**VertexMarker.cs**
- Sem mudancas. Novas esferas criadas pelo FaceExtrude, nao pelo VertexMarker.

## Fluxo de interacao

```
Mao perto do centro da face
  → FaceSelector detecta, destaca 4 esferas (amarelo)
  → HandModeManager: mode = Extrude
  → Pinch ativa
  → FaceExtrude: duplica vertices, cria triangulos, spawna esferas
  → Mao puxa na direcao da normal
  → FaceExtrude move novos vertices, VertexHUD mostra depth
  → Solta → extrude completo, MeshDeformer.RebuildVertexMap()
  → Novas esferas editaveis via MagneticSnapping
  → Nova face extrudavel via FaceSelector
```

## Deteccao face vs vertice

Criterio relativo, nao absoluto:
- Calcula distancia da mao ao centroide de cada face
- Calcula distancia da mao ao vertice mais proximo
- Se centroide mais perto → face mode
- Se vertice mais perto → ignora (MagneticSnapping cuida)
- Funciona em qualquer escala de objeto

## Riscos

- **Topologia suja:** extrudes repetidos podem gerar vertices sobrepostos se o usuario extrudar e depois empurrar a face de volta. Mitigacao: aceitar por agora, merge de vertices e cleanup fica pra polish futuro.
- **Performance:** cada extrude adiciona 4 vertices + 8 triangulos + 4 esferas. Depois de muitos extrudes, RecalculateNormals() e o loop do MeshDeformer podem ficar pesados. Mitigacao: monitorar, otimizar se necessario.
- **Normals invertidas:** a ordem dos vertices nos novos triangulos precisa ser consistente (clockwise) pra normal apontar pra fora. Se invertida, a face fica invisivel (backface culling). Testar cuidadosamente.
