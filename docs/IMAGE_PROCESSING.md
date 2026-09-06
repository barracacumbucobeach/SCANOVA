# Processamento de imagem

> **Status:** parcialmente disponível desde a Fase 2 (`SCANOVA.Imaging`); binarização e
> melhoria automática completa chegam na Fase 3/5.

## SCANOVA.Imaging (Fase 2)

Implementado com SkiaSharp (MIT). Serviços:

- `IImageLoader` (`SkiaImageLoader`) — decodifica JPG, JPEG, PNG, BMP, GIF, WEBP para
  `RasterImage` (RGBA32).
- `IImageExporter` (`SkiaImageExporter`) — grava `RasterImage` como PNG/JPG/BMP.
- `IImageService` (`SkiaImageService`) — rotação (ângulo arbitrário, com expansão de tela),
  espelhamento horizontal/vertical, corte por caixa delimitadora, conversão para escala de
  cinza (luminância Rec. 601), normalização de DPI (redimensionamento preservando o tamanho
  físico).

`RasterImage` (em `SCANOVA.Core.Models`) é a representação de imagem independente de
biblioteca usada em toda a aplicação — ver `docs/ARCHITECTURE.md`.

## Próximas fases

- Fase 3: binarização (Otsu/adaptativa) e conversão para 1-bit, para o pipeline TIFF.
- Fase 5: detecção de documento, correção de perspectiva, deskew, remoção de fundo/ruído,
  motor completo de melhoria automática (`IDocumentEnhancementService`).
