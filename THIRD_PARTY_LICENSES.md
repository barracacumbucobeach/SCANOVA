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
| NAPS2.Wia | 2.0.3 | MIT (`PackageLicenseExpression` no .csproj do pacote, verificado no código-fonte do repositório) | Wrapper de baixo nível para Windows Image Acquisition (WIA 1.0/2.0), mantido pelo projeto NAPS2 — evita reimplementar/adivinhar constantes de propriedade e GUIDs do WIA via COM tardio (seção 9) | https://github.com/cyanfish/naps2-wia |
| PDFsharp | 6.2.4 | MIT (verificado em `LICENSE` na raiz do repositório) | Geração de PDF (páginas de imagem, tamanho físico calculado a partir do DPI) | https://github.com/empira/PDFsharp |
| PDFtoImage | 5.4.0 | MIT (verificado em `LICENSE` e no `PackageLicenseExpression` do .csproj do pacote) | Rasterização de páginas PDF existentes em imagem, via PDFium — roda de forma idêntica em Windows/Linux/macOS | https://github.com/sungaila/PDFtoImage |
| pdfium-binaries (empacotamento do PDFium usado pelo PDFtoImage) | acompanha a versão do PDFtoImage | MIT (scripts de build) sobre o motor PDFium do Chromium, licenciado Apache License 2.0 (verificado em `LICENSE` no repositório `chromium/pdfium`) | Motor de renderização de PDF nativo (PDFium/Chromium) | https://github.com/bblanchon/pdfium-binaries |
| PdfPig | 0.1.16 | Apache 2.0 (verificado em `LICENSE`; inclui componentes de terceiros sob licença BSD — PDFBox/FontBox) | Extração de texto NATIVO de um PDF que já contém texto real, sem OCR (seção 109) | https://github.com/UglyToad/PdfPig |
| NAPS2.Tesseract.Binaries | 1.4.0 | Apache 2.0 (verificado no `PackageLicenseExpression` do .csproj do pacote e no `LICENSE` do repositório — repositório **separado** do aplicativo NAPS2 principal, ver nota em "Explicitamente evitadas") | Executáveis nativos reais do motor Tesseract OCR (Windows x86/x64/ARM64, Linux x64/ARM64, macOS x64/ARM64), invocados como processo externo (nunca P/Invoke) via linha de comando + saída hOCR (seção 40/107) | https://github.com/cyanfish/naps2-tesseract |
| tessdata_fast (modelos de idioma do Tesseract) | N/A (dados, baixados sob demanda em tempo de execução, não empacotados) | Apache 2.0 (verificado em `LICENSE` no repositório) | Modelos LSTM "rápidos" oficiais do Tesseract para reconhecimento de texto impresso, em português/inglês/espanhol + orientação (osd) — baixados e armazenados em cache local na primeira vez que um idioma é usado (seção 41) | https://github.com/tesseract-ocr/tessdata_fast |
| DocumentFormat.OpenXml | 3.5.1 | MIT (verificado em `LICENSE` no repositório) | Exportação do texto reconhecido para DOCX (Word) | https://github.com/dotnet/Open-XML-SDK |
| Noto Sans (fonte, `NotoSans[wdth,wght].ttf`) | N/A (fonte, arquivo estático embutido como recurso) | SIL Open Font License 1.1 (verificado em `OFL.txt`, incluído junto do arquivo da fonte) | Fonte embutida no assembly `SCANOVA.Pdf` para desenhar a camada de texto invisível/pesquisável do PDF (seção 109-111) — necessária porque o build do PDFsharp usado fora do Windows não tem acesso a nenhuma fonte do sistema por padrão | https://github.com/google/fonts/tree/main/ofl/notosans |
| System.Security.Cryptography.ProtectedData | 8.0.0 | MIT (parte do próprio repositório dotnet/runtime, mesma licença já usada por Microsoft.Extensions.DependencyInjection acima) | Publica a DPAPI (Windows Data Protection API) para .NET moderno — só usada por `SecureLicenseStore` para criptografar a licença ativada em disco (Fase 10, seção: armazenamento seguro) | https://github.com/dotnet/runtime |
| WiX Toolset | 5.0.2 | MS-RL (Microsoft Reciprocal License — verificada no `PackageLicenseExpression` do pacote `wix`/`WixToolset.Sdk` no NuGet, versão 5.0.2). Ferramenta de **build**, nunca distribuída com o aplicativo nem vinculada a ele (o instalador `.msi` que ela produz não contém nenhum código do WiX) — mesmo raciocínio já aplicado a `Microsoft.Windows.SDK.BuildTools` acima: um compilador não "contamina" a licença do que ele compila. **Fixado deliberadamente em 5.0.2, nunca 6.x/7.x**: a partir da v6.0.0 o pacote passou a exigir aceitar um EULA adicional (OSMFEULA — "Open Source Maintenance Fee") sobre o binário pré-compilado, cobrando uma taxa mensal de usuários com faturamento anual ≥ US$10.000 — incompatível com "nunca cobrança recorrente" (seção 146). A v5.0.2 (última da série 5.x, que já traz o recurso `<Files>` usado em `Product.wxs`) continua sob MS-RL puro, sem esse EULA — verificado comparando o `nuspec` de várias versões (`license type="expression" MS-RL` até 5.0.2, `license type="file" OSMFEULA.txt` a partir de 6.0.0) | Gera o instalador tradicional (MSI) do SCANOVA (Fase 11, seção 91/118) | https://github.com/wixtoolset/wix |

## Bibliotecas próprias (não distribuídas com o aplicativo)

| Ferramenta | Onde vive | Por que não é uma dependência de terceiros | Nunca faz parte de |
|---|---|---|---|
| ECDSA P-256 (assinatura/verificação de licenças) | `System.Security.Cryptography` (BCL do próprio .NET, sem pacote NuGet adicional) | Já faz parte do runtime .NET incluído no aplicativo — nenhuma licença de terceiro a mais para documentar | A chave PRIVADA de assinatura nunca faz parte de nenhum artefato distribuído — ver `docs/LICENSING.md` |

## Explicitamente evitadas

- **SixLabors.ImageSharp (v3+)** — a partir da v3 adota a "Six Labors Split License", que exige
  licenciamento comercial pago acima de um determinado faturamento anual do licenciado. Para
  evitar essa obrigação comercial, o processamento de imagem usa **SkiaSharp (MIT)** em seu
  lugar, que cobre o mesmo escopo sem essa restrição.
- **NAPS2.Sdk / repositório principal `cyanfish/naps2`** — é o wrapper .NET "óbvio" para
  Tesseract (usado pelo próprio aplicativo NAPS2), mas esse repositório é licenciado **GPL-2.0**,
  incompatível com o modelo de licenciamento comercial fechado do SCANOVA (seção 146) se
  referenciado como dependência. Os **binários nativos** do Tesseract usados pelo SCANOVA vêm de
  um repositório **separado e distinto**, `cyanfish/naps2-tesseract` (Apache 2.0 — linha acima);
  o wrapper C# que os invoca (`SCANOVA.Ocr.TesseractOcrService`) é código original do SCANOVA,
  escrito contra a interface de linha de comando pública e genérica do próprio Tesseract, sem
  copiar nenhum código-fonte do NAPS2.

## Processo

Antes de adicionar qualquer nova dependência ao projeto, registrar aqui: nome, versão, licença,
finalidade, link oficial e alternativas consideradas (seção 139), e confirmar que a licença
permite distribuição comercial fechada do SCANOVA sem obrigações incompatíveis com o modelo de
licença vitalícia (seção 146).
