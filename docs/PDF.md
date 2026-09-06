# PDF — geração, rasterização e conversão

> **Status:** Fase 6 concluída — leitura/rasterização de PDFs existentes, geração de PDF
> imagem-only (Documental/Cor/Escala de cinza), conversão TIFF → PDF. A camada de texto
> invisível de OCR (PDF pesquisável) fica para a Fase 9 (seção 109).

## Bibliotecas (ver `THIRD_PARTY_LICENSES.md`)

- **PDFsharp** (MIT) — escrita de PDF. Usada só no nível de objeto de página/imagem
  (`PdfDocument`, `PdfPage`, `XGraphics.DrawImage`) — nenhum texto é desenhado por ela nesta
  fase (ver "Por que não há camada de texto ainda", abaixo).
- **PDFtoImage** (MIT, sobre o motor PDFium do Chromium — Apache 2.0/BSD) — rasterização de
  páginas de um PDF existente em imagem. Já traz o SkiaSharp consigo (devolve `SKBitmap`
  diretamente) e roda de forma **idêntica em Windows/Linux/macOS**, sem depender de GDI+/WPF —
  por isso os testes deste projeto rasterizam PDFs de verdade neste ambiente Linux, e não apenas
  verificam "degradação graciosa" como o scanner WIA (Windows-only).
- **PdfPig** (Apache 2.0) — extração de texto **nativo** de um PDF que já contém texto real
  (nunca OCR).

## SCANOVA.Pdf.PdfWriter.PdfSharpPdfService (`IPdfService`)

Gera um PDF "imagem-only": cada página é uma imagem (PNG ou JPEG) embutida, do **tamanho físico
exato** calculado a partir do DPI da própria imagem (`largura_px / DPI` polegadas × 72 pontos —
1 ponto PDF = 1/72 polegada), não um tamanho de papel fixo. Isso garante que abrir o PDF em
qualquer leitor mostre o documento na escala 1:1 correta, exatamente como o TIFF documental.

- **Formato de codificação por conteúdo, não por modo:** páginas com poucas cores (preto e
  branco 1-bit, escala de cinza) usam **PNG** (sem perdas — JPEG introduziria "ringing" ao redor
  de bordas de texto); páginas coloridas usam **JPEG** (qualidade 85), a escolha padrão do
  mercado para digitalizações fotográficas coloridas, por um tamanho de arquivo menor.
- **Validação de formato×modo**, no mesmo espírito de `LibTiffEncoder.ValidatePageAgainstSettings`:
  `PdfMode.Documental` exige `Bilevel1`, `Grayscale` exige `Gray8`, `Color` exige `Rgb24`/`Rgba32`
  — pega um engano do chamador cedo, com uma mensagem clara, em vez de gerar um PDF incoerente.
- **`ConvertTiffToPdfAsync`** decodifica o TIFF (via `ITiffEncoder.DecodeAsync`, reaproveitando o
  decoder já existente da Fase 3 — nenhuma lógica de leitura de TIFF duplicada) e delega para
  `WritePdfAsync`, preservando todas as páginas na ordem (seção 31).
- **`TryExtractTextAsync`** abre o PDF com PdfPig e devolve `page.Text`; retorna `null` (não uma
  string vazia) quando não há texto nativo — o caso comum de um PDF imagem-only — para o
  chamador distinguir "sem texto" de "o OCR ainda não rodou" (seção 109).

### Por que não há camada de texto ainda

`IPdfService.WritePdfAsync` já aceita `ocrTextPerPage` (para a Fase 9 poder plugar sem quebrar o
contrato), mas pedir `PdfMode.Searchable`/`IncludeOcrTextLayer` hoje falha com uma mensagem clara
("será implementado na Fase 9") em vez de gerar um PDF incompleto. Dois motivos:

1. **Escopo:** "PDF pesquisável" está explicitamente listado como entregável da Fase 9 (seção
   109), junto do motor de OCR — faz sentido implementá-los juntos, quando existirem as posições
   de cada palavra reconhecida (o parâmetro atual só recebe uma string por página, sem
   coordenadas).
2. **Fontes no build "CORE" (sem GDI+) do PDFsharp:** desenhar texto via `XGraphics.DrawString`
   exige um `IFontResolver` registrado globalmente (`GlobalFontSettings.FontResolver`) — em
   Linux/macOS não há garantia de nenhuma fonte do sistema disponível, e o próprio PDFsharp não
   embute nenhuma por padrão fora do Windows. Configurar isso corretamente (bundlar uma fonte
   com licença permissiva, implementar o resolver) é trabalho real que só vale a pena fazer uma
   vez, junto da Fase 9.

## SCANOVA.Pdf.PdfRasterizer.PdfToImagePdfRasterizer (`IPdfRasterizer`)

`GetPageCountAsync`/`RasterizePageAsync`/`RasterizeAllAsync` — wrappers finos sobre
`PDFtoImage.Conversion`, convertendo o `SKBitmap` resultante para `RasterImage` (Rgba32). O DPI
solicitado é passado diretamente para o PDFium via `RenderOptions.Dpi` — a página é rasterizada
exatamente nessa resolução, independentemente do DPI "nativo" das imagens originais embutidas.

`RasterizeAllAsync` é um `IAsyncEnumerable<RasterImage>` verdadeiro (uma página é rasterizada e
entregue de cada vez, sem carregar o PDF inteiro em memória de uma vez) — importante para a Fase
8 (conversão em lote), quando PDFs grandes de muitas páginas passarem por aqui.

## Testes (`SCANOVA.Pdf.Tests`)

Só o projeto de testes referencia `SCANOVA.Tiff` — para gerar TIFFs sintéticos de verdade (via
`LibTiffEncoder` real, não um mock) e testar `ConvertTiffToPdfAsync` de ponta a ponta.

O teste de `TryExtractTextAsync` contra um PDF com texto real usa `MinimalTextPdfBuilder`: monta
os *bytes* de um PDF válido de uma página diretamente (objetos, tabela `xref` com offsets
calculados, `trailer`), referenciando a fonte padrão **Helvetica** (uma das 14 fontes padrão do
próprio formato PDF — qualquer leitor a reconhece por nome, sem precisar de nenhum arquivo de
fonte embutido). Isso evita completamente o problema do `IFontResolver` descrito acima: o teste
não pede ao PDFsharp para desenhar texto, só verifica que o extrator (PdfPig) lê corretamente um
PDF que contém texto de verdade.

## Próximas fases

- Fase 8: `RasterizeAllAsync`/`ConvertTiffToPdfAsync` entram no pipeline de conversão em lote.
- Fase 9: camada de texto invisível de OCR (PDF pesquisável), com posições reais por palavra.
