# Licenças de terceiros

Este documento lista as dependências de terceiros utilizadas pelo SCANOVA, sua licença, sua
finalidade e por que foram escolhidas (seções 104, 115, 116, 139 da especificação). **Nenhuma
dependência é adicionada ao projeto sem que sua licença seja verificada e registrada aqui
antes.**

Todas as bibliotecas abaixo têm licenças permissivas (MIT/BSD/Apache 2.0), compatíveis com
distribuição comercial fechada sem exigir a abertura do código-fonte do SCANOVA.

## Bibliotecas em uso

| Biblioteca | Versão | Licença | Finalidade | Link oficial |
|---|---|---|---|---|
| SkiaSharp | 2.88.8 | MIT | Decodificação/codificação de JPG, PNG, BMP, GIF, WEBP; operações de crop, rotação, redimensionamento, conversão de cor | https://github.com/mono/SkiaSharp |
| Serilog | 3.1.1 | Apache 2.0 | Logging técnico local em arquivo (seção 59) | https://github.com/serilog/serilog |
| Serilog.Sinks.File | 5.0.0 | Apache 2.0 | Sink de arquivo (rotação diária) para o Serilog | https://github.com/serilog/serilog-sinks-file |
| Microsoft.Extensions.DependencyInjection(.Abstractions) | 8.0.1 / 8.0.2 | MIT | Container de injeção de dependência | https://github.com/dotnet/runtime |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | MVVM (ObservableObject, RelayCommand) para ViewModels da UI WinUI 3 | https://github.com/CommunityToolkit/dotnet |
| Microsoft.WindowsAppSDK | 2.4.0 | MIT | WinUI 3 / Windows App SDK — framework de UI do aplicativo desktop | https://github.com/microsoft/WindowsAppSDK |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | Licença da Microsoft (uso permitido para build/distribuição de apps Windows) | Ferramentas de build referenciadas pelo Windows App SDK | https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools |
| xunit / xunit.runner.visualstudio | 2.4.x | Apache 2.0 / MIT | Testes automatizados (seções 75-77) | https://github.com/xunit/xunit |
| coverlet.collector | 6.0.0 | MIT | Cobertura de testes | https://github.com/coverlet-coverage/coverlet |
| BitMiracle.LibTiff.NET | 2.4.660 | Licença estilo BSD-3-Clause (texto completo verificado em https://raw.githubusercontent.com/BitMiracle/libtiff.net/master/license.txt — Copyright Bit Miracle, com trechos herdados do libtiff original de Sam Leffler/Silicon Graphics e do IJG). Permite uso comercial e redistribuição, exige apenas manter o aviso de copyright; proíbe usar o nome "Bit Miracle" para endosso. Compatível com distribuição comercial fechada. | Encoder/decoder TIFF, 1-bit, CCITT Group 4 — requisito comercial crítico do produto (seção 22/23/27) | https://github.com/BitMiracle/libtiff.net |

## Planejadas para as próximas fases (a confirmar/registrar antes do uso)

| Biblioteca | Licença (a confirmar na fase correspondente) | Finalidade | Fase |
|---|---|---|---|
| PDF: candidatos — PDFsharp (MIT) para escrita; PDFtoImage/Docnet.Core ou PDFium (Apache 2.0/BSD) para rasterização; PdfPig (Apache 2.0) para extração de texto nativo | A confirmar | Leitura/rasterização/geração de PDF | Fase 6 |
| Tesseract (motor OCR) + wrapper .NET (ex.: charlesw/tesseract, MIT) | Apache 2.0 (Tesseract) / MIT (wrapper) | OCR local, offline, com modelo em português | Fase 9 |
| DocumentFormat.OpenXml | MIT | Exportação de texto reconhecido para DOCX | Fase 9 |

## Explicitamente evitadas

- **SixLabors.ImageSharp (v3+)** — a partir da v3 adota a "Six Labors Split License", que exige
  licenciamento comercial pago acima de um determinado faturamento anual do licenciado. Para
  evitar essa obrigação comercial, o processamento de imagem usa **SkiaSharp (MIT)** em seu
  lugar, que cobre o mesmo escopo sem essa restrição.

## Processo

Antes de adicionar qualquer nova dependência ao projeto, registrar aqui: nome, versão, licença,
finalidade, link oficial e alternativas consideradas (seção 139), e confirmar que a licença
permite distribuição comercial fechada do SCANOVA sem obrigações incompatíveis com o modelo de
licença vitalícia (seção 146).
