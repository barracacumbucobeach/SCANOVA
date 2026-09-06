# Arquitetura

## Visão geral

SCANOVA é organizado em camadas, cada uma em seu próprio projeto .NET, para separar UI,
domínio, processamento e infraestrutura (princípio de desenvolvimento #2 da especificação):

```text
SCANOVA.App            (UI — WinUI 3, Windows-only)
   │
   ├── depende de → SCANOVA.Infrastructure, SCANOVA.Core
   │                (e, nas próximas fases: Imaging, Tiff, Pdf, Batch, Ocr, Scanner, Licensing)
   │
SCANOVA.Imaging / Tiff / Pdf / Batch / Ocr / Scanner / Licensing   (processamento — .NET 8 puro)
   │
   └── dependem de → SCANOVA.Core

SCANOVA.Infrastructure  (logging, configurações, arquivos temporários — .NET 8 puro)
   └── depende de → SCANOVA.Core

SCANOVA.Core            (modelos, enums, interfaces, exceções — sem dependências de terceiros)
```

Regra de dependência: **tudo depende de `Core`; `Core` não depende de nada além da BCL do
.NET.** Isso mantém o domínio livre de qualquer biblioteca de imagem/PDF/OCR específica —
por isso `SCANOVA.Core.Models.RasterImage` é uma representação de imagem em memória própria
(buffer de pixels + formato + DPI), não um tipo do SkiaSharp, do LibTiff.NET ou de qualquer
outra biblioteca. Isso permite trocar a biblioteca de imagem/TIFF/PDF no futuro sem alterar
`Core` nem os contratos (`IImageLoader`, `ITiffEncoder`, etc.).

## Por que a UI não compila fora do Windows

`SCANOVA.App` usa WinUI 3 (Windows App SDK). O compilador de marcação XAML
(`XamlCompiler.exe`, distribuído pelo pacote `Microsoft.WindowsAppSDK.WinUI`) é um executável
nativo do Windows — não há build nativo para Linux/macOS. Por isso:

- `SCANOVA.App.csproj` tem `TargetFramework=net8.0-windows10.0.19041.0` e só compila no Windows
  (com Visual Studio 2022 + workload "Windows App SDK C#/WinRT").
- Todas as demais camadas (`Core`, `Infrastructure`, `Imaging`, `Tiff`, `Pdf`, `Ocr`, `Scanner`,
  `Licensing`) têm `TargetFramework=net8.0` puro — compilam e têm testes reais executados em
  Linux, macOS e Windows.
- `SCANOVA.CrossPlatform.slnf` é um filtro de solução que inclui tudo exceto `SCANOVA.App`, usado
  para build/test em CI e em ambientes sem Windows.

## Modelo de implantação da UI

`SCANOVA.App` usa o modelo **"Unpackaged"** (`WindowsPackageType=None`) nesta fase — evita exigir
um `Package.appxmanifest` com ícones MSIX definitivos antes de termos os assets de marca finais.
O empacotamento (MSIX e/ou instalador tradicional) será definido na Fase 11 (seção 91 da
especificação), quando também trocaremos para o modelo empacotado, se for a opção escolhida.

## Injeção de dependência

Todo serviço "importante" (scanner, imagem, TIFF, PDF, OCR, licenciamento, configurações, log,
histórico) é exposto como interface em `SCANOVA.Core.Interfaces` (seção 81 da especificação) e
implementado na camada de infraestrutura/processamento correspondente. A composição acontece em
`SCANOVA.App/App.xaml.cs`, usando `Microsoft.Extensions.DependencyInjection`. Cada camada de
processamento expõe um método de extensão `IServiceCollection.AddScanovaXxx()` (ver
`SCANOVA.Infrastructure.ServiceCollectionExtensions` como exemplo) para que o composition root
não precise conhecer detalhes de implementação.

## Pipeline de processamento

O núcleo do produto é um pipeline configurável (seção 84/85 da especificação):

```text
Input → Decode → Detect/Crop/Perspective/Deskew (opcionais) → Grayscale → Enhancement
      → Resize/DPI → Color conversion → Binarization → Output encoder → Validation → Save
```

Cada etapa é um serviço de domínio isolado (`IDocumentDetectionService`,
`IDocumentEnhancementService`, `IImageService`, `ITiffEncoder`, `ITiffValidator`, ...), permitindo
compor pipelines diferentes por preset (TIFF Documental, PDF Documental, Documento Legível,
Colorido) sem duplicar lógica.

## Fases

O desenvolvimento segue a ordem definida na especificação (seções 135-137): cada fase é
compilada, testada e documentada antes de avançar para a próxima. Ver `README.md` para o status
atual.
