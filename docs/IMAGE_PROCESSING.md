# Processamento de imagem

> **Status:** Fase 2 e binarização (parte da Fase 3) concluídas; melhoria automática completa
> (deskew, perspectiva, remoção de fundo/ruído) chega na Fase 5.

## SCANOVA.Imaging (Fase 2)

Implementado com SkiaSharp (MIT). Serviços:

- `IImageLoader` (`SkiaImageLoader`) — decodifica JPG, JPEG, PNG, BMP, GIF, WEBP para
  `RasterImage` (RGBA32).
- `IImageExporter` (`SkiaImageExporter`) — grava `RasterImage` como PNG/JPG (o Skia não possui
  encoder de BMP, só decoder — por isso BMP não é um formato de exportação, consistente com o
  menu "Salvar como" da especificação, seção 32).
- `IImageService` (`SkiaImageService`) — rotação (ângulo arbitrário, com expansão de tela),
  espelhamento horizontal/vertical, corte por caixa delimitadora, conversão para escala de
  cinza (luminância Rec. 601), normalização de DPI (redimensionamento preservando o tamanho
  físico, com formato de pixel original preservado — Gray8/Bilevel1 não "vazam" para RGBA), e
  binarização (Otsu automático, global manual, adaptativo — ver `docs/TIFF.md`).

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

## Próximas fases

- Fase 5: detecção de documento, correção de perspectiva, deskew, remoção de fundo/ruído,
  motor completo de melhoria automática (`IDocumentEnhancementService`).
