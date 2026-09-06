# SCANOVA — Digitalização e Conversão Documental

> Digitalize. Aprimore. Converta. Organize.

SCANOVA é um aplicativo desktop Windows (WinUI 3 / Windows App SDK, .NET 8) para digitalização,
tratamento, conversão, composição e OCR de documentos, com foco especial na geração de **TIFF
200 DPI, 1-bit, com compressão CCITT Group 4** — o formato exigido por muitos sistemas legados de
gestão documental.

Modelo comercial: **licença vitalícia** por versão adquirida (sem assinatura, sem mensalidade).

## Status do desenvolvimento

O projeto é construído em fases (ver `docs/ARCHITECTURE.md` e o histórico de commits). Estado
atual:

- [x] **Fase 1 — Fundação**: solução, projetos por camada, injeção de dependência, logging
      local (Serilog), configurações persistidas (JSON), casca de navegação e Dashboard
      (WinUI 3).
- [x] **Fase 2 — Imagens**: abrir (JPG/PNG/BMP/GIF/WEBP), visualizar com zoom (ajustar à tela /
      1:1), girar, cortar (seleção manual), exportar (PNG/JPG). Decodificação/codificação e
      transformações rodam fora da UI thread (seção 7/61).
- [x] **Fase 3 — TIFF**: pipeline documental completo (escala de cinza → normalização de DPI →
      binarização Otsu/global/adaptativa → CCITT Group 4 → validação), via
      BitMiracle.LibTiff.NET. `ITiffDocumentPipeline` conectado ao "Salvar como" do
      visualizador — marco funcional da seção 152 (abrir imagem → TIFF Documental → validado).
- [x] **Fase 4 — Scanner**: `IScannerService` via WIA (Windows Image Acquisition), usando a
      biblioteca `NAPS2.Wia` (MIT) em vez de COM tardio — evita adivinhar constantes/GUIDs do
      WIA. Descoberta de scanners, seleção de origem (mesa/alimentador/duplex), catálogo de
      perfis (seção 11), tela de digitalização com progresso, degradação graciosa quando WIA
      está indisponível.
- [x] **Fase 5 — Automação**: detecção de documento (segmentação por Otsu + fecho convexo +
      retângulo de área mínima via "rotating calipers"), correção de perspectiva (homografia de
      Heckbert), deskew (estimador por perfil de projeção), remoção de fundo/ruído,
      brilho/contraste/gamma/saturação/nitidez, binarização com remoção de pequenas manchas —
      tudo orquestrado por `IDocumentEnhancementService` com os presets "Leve/Normal/Forte".
      "Melhorar automaticamente" e "Reverter para original" no visualizador.
- [x] **Fase 6 — PDF**: geração de PDF imagem-only (PDFsharp, MIT) com o tamanho físico exato
      calculado a partir do DPI de cada página; rasterização de PDFs existentes (PDFtoImage sobre
      PDFium, MIT/Apache 2.0) — roda de forma idêntica em Windows/Linux/macOS, sem dependência de
      GDI+; extração de texto nativo de um PDF que já contém texto, sem OCR (PdfPig, Apache 2.0);
      conversão TIFF → PDF preservando todas as páginas. "Documento PDF" agora é uma opção real
      em "Salvar como" no visualizador. A camada de texto invisível de OCR (PDF pesquisável) fica
      para a Fase 9, quando o motor de OCR fornecer o texto e a posição de cada palavra.
- [x] **Fase 7 — Composição frente/verso**: `IDuplexCompositionService` intercala páginas de
      frente e verso escaneadas/abertas em duas passagens separadas (frente 1, verso 1, frente
      2, verso 2, ...), com opções para inverter a ordem do verso (fluxo de duplex manual mais
      comum) e girar o verso 180°. Nova tela "Frente e verso" (Dashboard → "Frente + verso"):
      carrega dois lotes de imagens e salva como TIFF Documental multipágina.
- [x] **Fase 8 — Conversão em lote**: `IBatchProcessingService` processa uma fila de arquivos um
      por vez (nunca carrega o lote inteiro em memória), preservando o nome de cada arquivo de
      origem no destino. Suporta TIFF Documental/genérico, PNG, JPG e PDF; uma falha em um item
      nunca derruba o lote (relatório final com sucesso/falha por item). Pausa/retomada
      (`SemaphoreSlim`, sem ocupar threads) e cancelamento cooperativo, mesmo enquanto pausado.
      Nova tela "Converter em lote" (Dashboard → "Converter").
- [ ] Fase 9 — OCR
- [ ] Fase 10 — Licenciamento
- [ ] Fase 11 — Polimento e instalador

## Estrutura

```text
SCANOVA.sln                     — solução completa (Visual Studio, Windows)
SCANOVA.CrossPlatform.slnf      — filtro de solução SEM o projeto de UI (WinUI 3);
                                   compila/testa em Linux/macOS/CI
src/
├── SCANOVA.App/                — UI WinUI 3 (Views, ViewModels, Controls). Só compila no Windows.
├── SCANOVA.Core/                — modelos, enums, interfaces e exceções de domínio (sem dependências de UI/infra)
├── SCANOVA.Imaging/             — carregamento e processamento de imagem (SkiaSharp)
├── SCANOVA.Tiff/                — encoder/validador TIFF, CCITT Group 4, 200 DPI
├── SCANOVA.Pdf/                 — leitura, rasterização e geração de PDF
├── SCANOVA.Batch/                — conversão em lote (fila, progresso, pausa/cancelamento)
├── SCANOVA.Ocr/                 — reconhecimento de texto local
├── SCANOVA.Scanner/             — abstração de digitalização (WIA no Windows)
├── SCANOVA.Licensing/           — licenciamento vitalício
└── SCANOVA.Infrastructure/      — logging, configurações, arquivos temporários, diagnóstico

tests/                          — um projeto de teste por camada + testes de integração
docs/                            — documentação técnica (arquitetura, TIFF, scanner, OCR, PDF, build, testes)
```

## Por que a UI não compila neste ambiente

`SCANOVA.App` usa WinUI 3 (Windows App SDK), cujo compilador de XAML é um executável nativo do
Windows — **não compila em Linux/macOS**. Todas as demais camadas (`Core`, `Infrastructure`,
`Imaging`, `Tiff`, `Pdf`, `Batch`, `Ocr`, `Scanner`, `Licensing`) são bibliotecas .NET 8 puras,
multiplataforma, com testes reais executados neste ambiente. Use
`SCANOVA.CrossPlatform.slnf` para compilar/testar tudo o que não depende de Windows. Veja
`docs/BUILD.md` para instruções completas de build em cada plataforma.

## Build rápido (Linux/macOS/CI — sem a UI)

```bash
dotnet restore SCANOVA.CrossPlatform.slnf
dotnet build SCANOVA.CrossPlatform.slnf
dotnet test SCANOVA.CrossPlatform.slnf
```

## Build completo (Windows, com a UI)

Requer Visual Studio 2022 com as workloads ".NET Desktop Development" e "Windows App SDK
C#/WinRT". Veja `docs/BUILD.md`.

## Licença

Software proprietário — ver `LICENSE`. Dependências de terceiros e suas licenças estão
documentadas em `THIRD_PARTY_LICENSES.md`.
