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
| SCANOVA.Core | — | a preencher conforme modelos ganham lógica não trivial |
| SCANOVA.Imaging | `SkiaImageLoaderTests`, `SkiaImageExporterTests`, `SkiaImageServiceTests`, `BinarizationTests` (33 testes: carregar/exportar, decodificar BMP, rotação, flips, crop, escala de cinza, normalização de DPI incl. preservação de formato Gray8/Bilevel1, Otsu, binarização global/adaptativa) | ✅ Fase 2/3 |
| SCANOVA.Tiff | `TiffEncoderTests`, `TiffValidatorTests`, `TiffDocumentPipelineTests` (23 testes: round-trip bilevel/gray/RGB exato, rejeição de combinações inválidas (G4 sem bilevel), multipágina, checklist de validação completo, marco funcional ponta-a-ponta seção 125/150 — imagem colorida→TIFF documental válido com DPI normalizado, não-destrutivo, compressão real, threshold global/adaptativo, erro de caminho amigável) | ✅ Fase 3 (crítico) |
| SCANOVA.Pdf | — | Fase 6 |
| SCANOVA.Ocr | — | Fase 9 |
| SCANOVA.Scanner | — | Fase 4 (com scanner mock) |
| SCANOVA.Integration.Tests | — | Fase 3+ (fluxos ponta-a-ponta) |
