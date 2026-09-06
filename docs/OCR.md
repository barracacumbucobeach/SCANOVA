# OCR (reconhecimento de texto)

> **Status:** Fase 9 concluída — motor Tesseract local, idiomas com foco em português do Brasil, editor de texto, exportação TXT/DOCX e PDF pesquisável.

## SCANOVA.Ocr.TesseractOcrService (`IOcrService`)

Reconhece texto **inteiramente local** (seção 40): nenhuma imagem ou texto é enviado a nenhuma
API/serviço externo. O motor é o executável real do **Tesseract OCR** (Apache 2.0), trazido
pelo pacote `NAPS2.Tesseract.Binaries` (binários nativos para Windows x86/x64/ARM64, Linux
x64/ARM64 e macOS x64/ARM64) — **não** confundir com o pacote `NAPS2.Sdk`/aplicativo NAPS2
principal, que é GPL-2.0 e nunca é usado ou referenciado aqui; os binários vêm de um repositório
separado (`cyanfish/naps2-tesseract`), com sua própria licença Apache 2.0 (ver
`THIRD_PARTY_LICENSES.md`).

O Tesseract é sempre invocado como **processo externo** (`System.Diagnostics.Process`), nunca
via P/Invoke — evita qualquer complexidade de interoperabilidade nativa e mantém o mesmo padrão
de isolamento usado para o WIA na Fase 4.

### Pipeline de reconhecimento

```text
RasterImage → PNG temporário (IImageExporter) → tesseract (CLI, hOCR) → HocrParser → OcrResult
```

1. A imagem de entrada é gravada num arquivo PNG temporário próprio (nunca o documento
   original) numa pasta exclusiva da chamada, apagada no `finally` mesmo se o reconhecimento
   falhar (seção 43).
2. O Tesseract roda com `-c tessedit_create_hocr=1`, gerando um arquivo **hOCR** (XHTML com
   posição e confiança por palavra) em vez de só texto simples — é o único formato de saída do
   Tesseract que preserva a posição de cada palavra, necessária para a camada de texto invisível
   do PDF pesquisável (ver `docs/PDF.md`).
3. `--psm 1` (segmentação automática de página **com** detecção de orientação/script, exige
   `osd.traineddata`) é usado quando `OcrSettings.CorrectOrientation` é verdadeiro;
   `--psm 3` (segmentação automática simples) caso contrário.
4. `HocrParser` (interno) interpreta o XML via `XDocument`/`XmlReader` com
   `DtdProcessing.Ignore` e `XmlResolver` nulo — o cabeçalho hOCR referencia um DTD XHTML
   externo que não precisa (nem deve) ser buscado pela rede só para interpretar o conteúdo.
   Extrai cada palavra (`ocrx_word`) com sua caixa delimitadora (`bbox x0 y0 x1 y1`) e confiança
   (`x_wconf`, 0-100 → normalizada para 0.0-1.0) do atributo `title`.

`RecognizeMultiPageAsync` chama `RecognizeAsync` página a página e concatena o texto na ordem
correta (separador de linha em branco quando `PreserveLineBreaks` é verdadeiro, espaço simples
caso contrário) — os blocos de todas as páginas são simplesmente concatenados na lista final.

### Idiomas e modelos (`TesseractLanguageDataProvider`)

Nenhum modelo de idioma (`.traineddata`) é embutido no instalador — alguns têm vários MB e a
maioria dos usuários só precisa de um ou dois idiomas. Em vez disso, o modelo é **baixado sob
demanda** (seção 41), na primeira vez que um idioma é usado, do repositório oficial
`tesseract-ocr/tessdata_fast` (Apache 2.0 — os modelos LSTM "rápidos" do próprio projeto
Tesseract, adequados para documentos impressos/digitalizados, o caso de uso do SCANOVA, não
manuscritos), e fica em cache permanente em `AppPaths.OcrLanguageDataFolder`.

| `OcrLanguage` | Código(s) Tesseract | Observação |
|---|---|---|
| `PortugueseBrazil` / `Portuguese` | `por` | Português é um único modelo no Tesseract (não distingue BR/PT). |
| `English` | `eng` | |
| `Spanish` | `spa` | |
| `Automatic` | `por+eng` | O Tesseract não faz identificação automática de idioma; combinar português (mercado principal) com inglês é um padrão razoável para "Automático". |

Quando `CorrectOrientation` é verdadeiro, o modelo adicional `osd.traineddata` (orientação/script,
~10,5 MB) também é baixado. `TESSDATA_PREFIX` aponta diretamente para a pasta de cache (sem
subpasta `tessdata`, convenção das versões modernas do Tesseract).

Falha de download (sem internet, por exemplo) vira um `OcrException` com mensagem amigável
pedindo para verificar a conexão — nunca uma exceção técnica crua (seção 60).

### Exportação de texto (`OpenXmlTextDocumentExporter`, `ITextDocumentExporter`)

- **TXT**: texto simples, UTF-8.
- **DOCX**: via `DocumentFormat.OpenXml` (MIT) — um parágrafo por linha, preservando as quebras
  de linha do texto reconhecido/editado (seção 111).

## PDF pesquisável (integração com a Fase 6)

Ver `docs/PDF.md` para o mecanismo completo — resumo: quando `PdfSettings.Mode` é
`PdfMode.Searchable` (ou `IncludeOcrTextLayer` é verdadeiro), `IPdfService.WritePdfAsync` recebe
também um `OcrResult` por página e desenha cada bloco reconhecido como texto **totalmente
transparente**, posicionado exatamente sobre a palavra correspondente na imagem — o PDF fica
visualmente idêntico a um PDF comum, mas o texto pode ser selecionado/copiado e encontrado por
busca, em praticamente qualquer leitor de PDF.

## SCANOVA.App — tela "Extrair texto" (Fase 9)

Nova página (Dashboard → cartão "Extrair texto", ou item de navegação "OCR"): `OcrViewModel`
permite abrir um documento (imagem), escolher o idioma, reconhecer o texto (com indicação de
progresso e da confiança média do resultado) e revisar/**corrigir manualmente** o texto antes de
salvar — a correção nunca altera a imagem original (edição não destrutiva, seção 45). A partir
do texto revisado, o usuário pode salvar como TXT, DOCX (Word) ou gerar um PDF pesquisável
diretamente (reaproveitando as posições originais do OCR para a camada invisível, já que a
imagem em si não muda com a correção do texto).

## Testes (`SCANOVA.Ocr.Tests`)

- **`TesseractOcrServiceTests`**: testes de ponta a ponta de verdade — o executável real do
  Tesseract (via `NAPS2.Tesseract.Binaries`) reconhece texto desenhado por código (fonte Noto
  Sans embutida, a mesma da camada de texto do PDF — nunca um documento de uma pessoa real,
  seção 122) em imagens sintéticas geradas com SkiaSharp: texto impresso comum, múltiplas
  linhas com preservação de quebra, página em branco (sem falhar), múltiplas páginas
  concatenadas em ordem, idiomas disponíveis localmente após o uso e cancelamento antes do
  início.
- **`HocrParserTests`**: interpretação de um arquivo hOCR "à mão" (mesmo formato real de saída
  do Tesseract) — palavras/posições/confiança extraídas corretamente, preservação ou não de
  quebra de linha, página sem nenhuma palavra e arquivo ausente.
- **`TesseractLanguageDataProviderTests`**: mapeamento de código por idioma e detecção de
  disponibilidade local a partir de uma pasta de cache isolada por teste, sem depender de rede.
- **`OpenXmlTextDocumentExporterTests`**: gravação e releitura real de TXT e DOCX (contagem e
  conteúdo de parágrafos, normalização de `\r\n`, criação automática da pasta de destino,
  cancelamento antes do início).

Em Linux, `SkiaSharp.NativeAssets.Linux.NoDependencies` precisa ser referenciado explicitamente
pelo projeto de testes (mesmo padrão de `SCANOVA.Imaging.Tests`/`SCANOVA.Tiff.Tests`) — sem ele,
`SKTypeface`/`SKBitmap` falham com `DllNotFoundException` ao rodar os testes fora do Windows.
