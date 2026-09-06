# Processamento de imagem

> **Status:** Fases 2, 3 (binarização) e 5 (automação/melhoria) concluídas.

## SCANOVA.Imaging (Fase 2)

Implementado com SkiaSharp (MIT). Serviços:

- `IImageLoader` (`SkiaImageLoader`) — decodifica JPG, JPEG, PNG, BMP, GIF, WEBP para
  `RasterImage` (RGBA32).
- `IImageExporter` (`SkiaImageExporter`) — grava `RasterImage` como PNG/JPG (o Skia não possui
  encoder de BMP, só decoder — por isso BMP não é um formato de exportação, consistente com o
  menu "Salvar como" da especificação, seção 32).
- `IImageService` (`SkiaImageService`) — rotação (ângulo arbitrário — `double`, não limitado a
  graus inteiros nem a múltiplos de 90°, com expansão de tela), espelhamento horizontal/vertical,
  corte por caixa delimitadora, conversão para escala de cinza (luminância Rec. 601),
  normalização de DPI (redimensionamento preservando o tamanho físico, com formato de pixel
  original preservado — Gray8/Bilevel1 não "vazam" para RGBA), binarização (Otsu automático,
  global manual, adaptativo — ver `docs/TIFF.md`), correção de perspectiva, brilho/contraste,
  gamma, saturação, nitidez, redução de ruído e remoção de fundo (ver Fase 5 abaixo).

`RasterImage` (em `SCANOVA.Core.Models`) é a representação de imagem independente de
biblioteca usada em toda a aplicação — ver `docs/ARCHITECTURE.md`.

### Threading (seção 7/61)

Decodificação, codificação e transformações de pixel podem ser custosas para imagens grandes de
scanner, então nunca rodam diretamente na UI thread:

- `SkiaImageLoader`/`SkiaImageExporter` despacham o trabalho síncrono do Skia via `Task.Run`
  internamente — quem chama `await loader.LoadAsync(...)` a partir da UI thread não trava a
  interface.
- `IImageService` é intencionalmente síncrono (função pura sobre pixels — mais simples de
  testar). É responsabilidade do chamador rodar em background quando invocado a partir da UI;
  `DocumentViewerViewModel` (App) faz isso com `Task.Run` antes de reconstruir o bitmap de tela.

## SCANOVA.App — visualizador/editor básico (Fase 2)

`DocumentViewerPage` + `DocumentViewerViewModel`: abrir uma imagem (`Dashboard` → "Abrir
documento", via `IFilePickerService`), visualizar com zoom (`ScrollViewer.ZoomMode`, botões
"Ajustar à tela"/"1:1"), girar 90° para os dois lados, cortar por seleção manual (arrastar um
retângulo sobre a imagem, "Aplicar corte"/"Cancelar corte") e exportar ("Salvar como" → **TIFF
Documental (CCITT Group 4 — 200 DPI)** / PNG / JPG — seção 66). Escolher TIFF Documental aciona
`ITiffDocumentPipeline` (ver `docs/TIFF.md`): processa, salva e valida automaticamente, sem o
usuário precisar entender DPI/CCITT/binarização (seção 131) — o marco funcional da seção 152.
Edição não destrutiva (seção 45): o arquivo original em disco só é tocado quando o usuário
confirma "Salvar como".

`RasterImageBitmapConverter` (App) converte `RasterImage` (RGBA32) para `WriteableBitmap`
(BGRA8, formato de pixel do WinUI 3) — fica em `SCANOVA.App`, não em `SCANOVA.Imaging`, porque é
a única camada com dependência de WinUI.

## Fase 5 — Automação: detecção de documento e melhoria automática

Ajustes de imagem adicionais em `SkiaImageService` (todos funções puras sobre o buffer de
pixels, sem dependência do SkiaSharp além da conversão inicial para RGBA quando necessário):

- `AdjustBrightnessContrast`/`AdjustGamma`/`AdjustSaturation` — fórmulas clássicas (contraste:
  `factor = 259(C+255) / (255(259−C))`; gamma via LUT de 256 entradas; saturação por
  interpolação de cada canal em direção à luminância).
- `Sharpen` (máscara de nitidez: original + amount×(original − desfocado)), `ReduceNoise` e
  `RemoveBackground` (estima o fundo por desfoque de raio grande e o subtrai, normalizando
  iluminação irregular) — todos construídos sobre `PixelOps`, um desfoque de média separável
  (horizontal + vertical) reaproveitado pelas três operações.
- `CorrectPerspective` — mapeia um quadrilátero arbitrário para um retângulo alinhado aos eixos
  via a homografia clássica "quadrado unitário → quadrilátero" (Heckbert, *Fundamentals of
  Texture Mapping and Image Warping*, 1989), com amostragem bilinear.

### Detecção de documento (`SCANOVA.Imaging.Detection`)

`IDocumentDetectionService` (`DocumentDetectionService`) segmenta o documento do fundo por
limiar de Otsu (a classe majoritária na borda da imagem é considerada fundo, então funciona com
documento claro sobre fundo escuro ou o inverso, sem assumir a polaridade), extrai os pontos do
contorno externo e calcula:

1. **Fecho convexo** (`ConvexHull`, algoritmo de Andrew — "monotone chain", O(n log n));
2. **Retângulo de área mínima** (`MinimumAreaRectangle`, "rotating calipers" — Toussaint, 1983)
   que envolve o fecho, cujo ângulo de aresta vira o `SkewAngleDegrees` do resultado e cujos
   quatro cantos viram o `CropRegion` (quadrilátero) detectado.

A confiança da detecção é a proporção de pixels de documento dentro do retângulo detectado
(`fillRatio`) — próxima de 1.0 para um retângulo bem definido, baixa para uma mancha irregular.
Detecções com área irrisória, quase 100% do quadro, ou confiança abaixo de um piso mínimo são
descartadas (`DocumentFound = false`) em vez de arriscar um corte ruim.

`ProjectionProfileSkewEstimator` (interno) é um segundo estimador de inclinação, independente da
detecção de borda: testa ângulos candidatos e escolhe aquele cuja projeção dos pixels de tinta é
mais "concentrada" em faixas estreitas (método de Postl, 1986) — funciona mesmo quando o
documento preenche todo o quadro (sem borda detectável, ex.: alimentador automático).

### Motor de melhoria (`SCANOVA.Imaging.Enhancement`)

`IDocumentEnhancementService` (`DocumentEnhancementService`) orquestra os primitivos acima em um
pipeline completo, na ordem: perspectiva (auto-detecção + recorte) → inclinação
(`ProjectionProfileSkewEstimator` + `Rotate`) → escala de cinza/remoção de fundo → redução de
ruído → tom (brilho/contraste/gamma) → nitidez → binarização + remoção de pequenas manchas
(`BilevelDespeckle`, componentes conectados por 8-conectividade menores que um limiar de área).
Cada etapa só roda quando o respectivo ajuste (`ImageAdjustments`) é solicitado; edição não
destrutiva — nunca modifica a imagem recebida.

`ResolvePreset` traduz os presets em linguagem simples (`EnhancementPreset.Light/Normal/Strong`)
em `ImageAdjustments` concretos; `Custom` retorna uma base neutra (os valores reais vêm dos
controles manuais da UI). `AutoEnhance` é `Apply(image, ResolvePreset(preset))`.

### SCANOVA.App — "Melhorar automaticamente" (Fase 5)

No visualizador (`DocumentViewerPage`), o cartão "Melhorar documento" do Dashboard abre o
documento e dois botões novos ficam disponíveis: **Melhorar automaticamente** (aplica
`AutoEnhance` com o preset Normal) e **Reverter para original** (edição não destrutiva — volta à
imagem original a qualquer momento antes de salvar).
