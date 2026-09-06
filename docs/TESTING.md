# Testes

## Estratégia

Um projeto de teste por camada de processamento (`tests/SCANOVA.<Camada>.Tests`), mais
`SCANOVA.Integration.Tests` para os fluxos ponta-a-ponta (seção 76) e
`SCANOVA.Infrastructure.Tests` para logging/configurações/temporários.

- **Dados de teste são sempre sintéticos**, gerados em código (nunca documentos reais de
  pessoas — seção 122).
- Mocks são permitidos apenas em testes (seção 140) — a aplicação final usa serviços reais,
  com a exceção documentada de um `IScannerService` mock disponível só no ambiente de testes.
- `SCANOVA.App` (WinUI 3) não é testável neste ambiente cross-platform; sua lógica de
  apresentação fica em ViewModels (`SCANOVA.App/ViewModels`) que dependem apenas de
  interfaces de `SCANOVA.Core`, o que permite testá-los no Windows com mocks das interfaces
  quando necessário.

## Executando

```bash
dotnet test SCANOVA.CrossPlatform.slnf
```

## Cobertura por camada (atualizado a cada fase)

| Camada | Testes | Status |
|---|---|---|
| SCANOVA.Infrastructure | `JsonSettingsServiceTests`, `TempFileManagerTests` | ✅ Fase 1 |
| SCANOVA.Core | `ScanProfileTests`, `RasterImageTests` (17 testes: catálogo de perfis seção 11, validação do construtor de RasterImage, cálculo de stride) | ✅ Fase 4 |
| SCANOVA.Imaging | `SkiaImageLoaderTests`, `SkiaImageExporterTests`, `SkiaImageServiceTests`, `BinarizationTests`, `AdjustmentsTests`, `PerspectiveTests`, `GeometryUtilsTests`, `ProjectionProfileSkewEstimatorTests`, `DocumentDetectionServiceTests`, `BilevelDespeckleTests`, `DocumentEnhancementServiceTests`, `DuplexCompositionServiceTests` (84 testes: carregar/exportar, decodificar BMP, rotação em ângulo arbitrário, flips, crop, escala de cinza, normalização de DPI incl. preservação de formato Gray8/Bilevel1, Otsu, binarização global/adaptativa, brilho/contraste/gamma/saturação/nitidez/redução de ruído/remoção de fundo, correção de perspectiva por homografia, fecho convexo, retângulo de área mínima, estimador de inclinação por perfil de projeção, detecção de documento com polaridade clara/escura, remoção de manchas por componentes conectados, pipeline completo de melhoria automática, composição frente/verso — intercalação, inversão de ordem, rotação de 180° só no verso, não-destrutivo, contagens incompatíveis) | ✅ Fase 2/3/5/7 |
| SCANOVA.Tiff | `TiffEncoderTests`, `TiffValidatorTests`, `TiffDocumentPipelineTests` (23 testes: round-trip bilevel/gray/RGB exato, rejeição de combinações inválidas (G4 sem bilevel), multipágina, checklist de validação completo, marco funcional ponta-a-ponta seção 125/150 — imagem colorida→TIFF documental válido com DPI normalizado, não-destrutivo, compressão real, threshold global/adaptativo, erro de caminho amigável) | ✅ Fase 3 (crítico) |
| SCANOVA.Pdf | `PdfSharpPdfServiceTests`, `PdfToImagePdfRasterizerTests` (17 testes: gerar PDF de página única/multipágina com tamanho físico correto a partir do DPI, ordem de páginas preservada, validação de formato×modo, mensagem clara para PDF pesquisável [Fase 9], conversão TIFF→PDF de ponta a ponta com o `LibTiffEncoder` real, extração de texto nativo via um PDF construído byte a byte — sem depender de nenhuma fonte do sistema — incluindo o caso "sem texto" de um PDF imagem-only, rasterização com escala de DPI correta, `RasterizeAllAsync` multipágina, erros de arquivo/página/DPI inválidos) | ✅ Fase 6 |
| SCANOVA.Ocr | — | Fase 9 |
| SCANOVA.Scanner | `WiaScannerServiceTests` (degradação graciosa sem WIA — cenário real deste CI Linux), `MockScannerServiceTests` (contrato de IScannerService com o mock da seção 140) — 7 testes | ✅ Fase 4 |
| SCANOVA.Integration.Tests | — | Fase 3+ (fluxos ponta-a-ponta) |
