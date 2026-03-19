# DEVLOG — LuminaXR

Diário técnico do projeto. Cada entrada documenta uma mecânica, decisão de arquitetura ou bug relevante.

---

## FEAT - Mecânica: Magnetic Snapping (Atração Magnética)

> **Dia: `10/03/2026`**

#### O Problema
> O tremor natural da mão humana dificulta tocar exatamente no pixel de um vértice 3D durante a modelagem espacial.

#### A Solução (Arquitetura)
> Uso de `Physics.OverlapSphere` em vez de colisão exata.
> - Disparamos uma esfera invisível (raio de ~5cm) a partir da ponta do dedo indicador (`IndexTip`).
> - O primeiro vértice detectado nessa área de tolerância é "puxado" magneticamente, compensando a falta de precisão motora.
> - **Feedback Visual:** Mudança de cor via `MaterialPropertyBlock` (Verde = Conectado / Cruz Vermelha = Solto).

#### Dampening (Filtro de Jitter)
> O SDK retorna a posição bruta da mão — com tremor. Aplicamos `Vector3.Lerp` a cada frame para suavizar o movimento.
> - `dampeningSpeed = 0.15` → move 15% da distância restante por frame
> - Resultado: movimentos rápidos ficam suavizados, movimentos lentos ficam precisos.

#### Decisões Técnicas
> - `MaterialPropertyBlock` em vez de `material.color` — evita instanciar materiais desnecessários.
> - `Shader.PropertyToID` cacheado como `static readonly int` — evita lookup por string a cada frame.
> - Detecção de pinch: distância entre `IndexTip` e `ThumbTip` < 3cm.

---

## FEAT - Mecânica: Vertex Markers (Marcadores de Vértice)

> **Dia: `10/03/2026`**

#### O Problema
> O usuário precisa visualizar e interagir com os vértices do cubo no espaço 3D.

#### A Solução (Arquitetura)
> `VertexMarker.cs` instancia esferas nas 8 quinas do cubo em runtime.
> - Posições hardcoded em coordenadas locais (`±0.5` em cada eixo — cubo unitário da Unity).
> - Esferas viram filhas do objeto no Hierarchy via `SetParent`.
> - Layer `VertexTarget` nas esferas — compatível com o `OverlapSphere` do `MagneticSnapping`.

#### Decisões Técnicas
> - Spawn no `Awake()` em vez de `Start()` — garante que as esferas existam antes do `MeshDeformer` rodar no `Start()`.

---

## FEAT - Mecânica: Mesh Deformer (Deformação de Malha)

> **Dia: `18/03/2026`**

#### O Problema
> Mover as esferas visuais não deforma o objeto — são objetos separados da mesh real.

#### A Solução (Arquitetura)
> `MeshDeformer.cs` conecta cada esfera aos vértices reais da mesh.
>
> **Fase 1 — Mapeamento (`BuildVertexMap`):**
> - Para cada esfera, encontra todos os vértices da mesh que estão na mesma posição (tolerância < 0.001f).
> - Armazena os índices em `int[][] vertexMap` — cada esfera sabe quais vértices ela controla.
> - Usa `InverseTransformPoint` para converter posição mundial → local antes de comparar com os vértices.
>
> **Fase 2 — Deformação (`Update`):**
> - A cada frame, move os vértices mapeados para a posição atual da esfera correspondente.
> - Após atualizar todos os vértices: `mesh.vertices = vertices` + `mesh.RecalculateNormals()`.

#### Por que `vertexMap` é `int[][]`?
> Um cubo tem vértices duplicados nas arestas (para normais diferentes). Uma esfera numa quina pode controlar até 3 vértices distintos no array interno da mesh. O mapa de índices resolve isso.

#### Decisões Técnicas
> - `localPos` calculado fora do loop interno — evita recalcular por vértice (era N_esferas × N_vértices chamadas, virou N_esferas).
> - `mesh.vertices = vertices` fora do loop das esferas — atualiza a mesh uma vez por frame, não uma vez por esfera.
> - `RecalculateNormals()` necessário após deformação — sem isso a iluminação quebra.

---

## FEAT - Mecânica: Object Grab + Preview Mode

> **Dia: `18/03/2026`**

#### O Problema
> O usuário precisa pegar o objeto inteiro para inspecionar a modelagem e mover pelo espaço.

#### A Solução (Arquitetura)
> `ObjectGrab.cs` detecta mão fechada e alterna entre dois estados via toggle.
>
> **Detecção de mão fechada:**
> - Mede distância das pontas dos 4 dedos (Index, Middle, Ring, Little) até a palma.
> - Se todas < 7cm → `isFist = true`.
>
> **Edge detection (borda):**
> - Variável `wasFist` guarda o estado do frame anterior.
> - Toggle só dispara no momento exato que a mão fecha (`isFist && !wasFist`), não a cada frame.
>
> **Modo Preview:**
> - Objeto flutua acima da palma com animação senoidal (`Mathf.Sin`) de subida/descida.
> - Rotação automática no eixo Y (`transform.Rotate`).
> - Fecha a mão de novo → solta o objeto.

#### Decisões Técnicas
> - `grabRange` — só ativa o grab se a palma estiver próxima do objeto (evita grab acidental de longe).
> - `floatHeight`, `bobHeight`, `bobSpeed`, `rotateSpeed` — todos expostos no Inspector para ajuste sem tocar no código.
> - `VertexMarker.Awake()` em vez de `Start()` — garante ordem de inicialização correta com `MeshDeformer`.
