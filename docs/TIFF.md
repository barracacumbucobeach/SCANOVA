# TIFF — CCITT Group 4, 200 DPI, 1-bit

> **Status:** Fase 3 concluída — o requisito comercial crítico do produto (seção 153: "o principal
> diferencial técnico/comercial é gerar documentos TIFF CCITT Group 4 em 200 DPI").

## Por que este formato

Muitos sistemas legados de gestão documental (cartórios, prefeituras, órgãos públicos,
digitalizadores) exigem TIFF **bilevel** (preto e branco puro, 1 bit por pixel) comprimido com
**CCITT Group 4** (também chamado de CCITT T.6) — o mesmo algoritmo usado historicamente por
aparelhos de fax. Ele é extremamente eficiente para texto/documentos escaneados (sem perdas,
compressão tipicamente de 10-30x sobre o bitmap bruto) e amplamente suportado por leitores TIFF.

## O pipeline (`SCANOVA.Tiff.TiffDocumentPipeline`)

```text
RasterImage (qualquer formato)
   ↓
Escala de cinza (IImageService.ToGrayscale)
   ↓
Normalização de DPI → 200 (IImageService.NormalizeDpi, sobre a imagem em cinza)
   ↓
Binarização (Otsu automático / global manual / adaptativo — IImageService.Binarize*)
   ↓
RasterImage Bilevel1 (1 bit, bit 1 = preto)
   ↓
Codificação TIFF (ITiffEncoder / LibTiffEncoder — CCITT Group 4)
   ↓
Validação (ITiffValidator / LibTiffValidator — reabre e confere tags)
   ↓
Arquivo TIFF documental válido
```

**Nota de implementação:** a normalização de DPI acontece sobre a imagem **em escala de cinza**,
antes da binarização — não é exatamente a ordem textual da seção 85 da especificação (que lista
"binarização" antes de "normalização de DPI"), mas produz bordas de texto muito melhores:
redimensionar um bitmap já binarizado sem uma etapa intermediária em tons de cinza produziria
serrilhado. O resultado final ainda satisfaz todos os checkpoints exigidos (200 DPI, 1 bit,
CCITT Group 4, validado) — seção 84 permite essa flexibilidade ("nem todas as etapas devem ser
obrigatórias").

## Binarização (`SCANOVA.Imaging`)

Três métodos, todos implementados em `SkiaImageService` (arquivo
`Binarization/SkiaImageService.Binarization.cs`), sem dependência do SkiaSharp — são algoritmos
puros sobre o buffer de pixels:

- **Otsu (automático/padrão)** — calcula um limiar global que maximiza a variância entre as
  classes "fundo" e "tinta" do histograma. Bom padrão para a maioria dos documentos.
- **Global manual** — um único limiar (0-255) escolhido pelo usuário (seção 89, modo avançado).
- **Adaptativo** — limiar calculado localmente (algoritmo de Bradley com imagem integral),
  melhor para documentos com iluminação irregular (ex.: foto de celular com sombra).

## Bilevel1 — convenção de bits

`RasterImage` com `PixelFormat.Bilevel1` empacota 8 pixels por byte, MSB primeiro. **Bit 1 =
preto (tinta), bit 0 = branco (fundo).** Isso corresponde exatamente à interpretação fotométrica
`PhotometricInterpretation = MinIsWhite` (valor de amostra 0 = branco) gravada no TIFF — a
convenção clássica para imagens estilo fax/CCITT (seção 25).

## Metadados gravados (seção 25)

| Tag TIFF | Valor |
|---|---|
| `ImageWidth` / `ImageLength` | dimensões em pixels |
| `BitsPerSample` | 1 |
| `SamplesPerPixel` | 1 |
| `PhotometricInterpretation` | MinIsWhite |
| `Compression` | CCITT Group 4 (T.6) |
| `XResolution` / `YResolution` | 200 |
| `ResolutionUnit` | Inch |
| `FillOrder` | MSB2LSB |
| `Software` | "SCANOVA" |

## Validação (`ITiffValidator` / `LibTiffValidator`)

Depois de gerar o TIFF, o validador **reabre o arquivo do zero** (não reaproveita nada da
codificação) e confirma, lendo as tags diretamente — a mesma prova que um leitor TIFF externo
enxergaria:

1. É um TIFF legível (`Tiff.Open` não falha).
2. É decodificável (`ReadScanline` funciona em todas as páginas).
3. `BitsPerSample == 1`.
4. `Compression == CCITT Group 4`.
5. `XResolution == YResolution == 200`.
6. `ResolutionUnit == Inch`.

Se qualquer item falhar, `ValidateDocumentalAsync` retorna um relatório com `IsValid = false` e
uma mensagem amigável — o arquivo original nunca é alterado (seção 26/60).

## Limitações conhecidas

- **RGBA em TIFF colorido**: ao codificar `ColorMode.Color` a partir de uma imagem RGBA32, o
  canal alfa é descartado (achatado para RGB24) — decisão deliberada para evitar a
  complexidade da tag `ExtraSamples`, já que o produto não tem caso de uso para TIFF colorido
  com transparência.
- **Decodificação genérica**: para TIFFs de terceiros com formatos não tratados explicitamente
  (paletizado, YCbCr/JPEG, 16-bit por amostra), `DecodeAsync` usa o *fallback* `ReadRGBAImage`
  do libtiff — funciona, mas sempre retorna RGBA32 (perde a profundidade de bits original).
  Formatos que o próprio SCANOVA produz (bilevel, escala de cinza 8-bit, RGB 24-bit) são sempre
  decodificados com fidelidade total.

## Bibliotecas utilizadas e licenças

**BitMiracle.LibTiff.NET** (porte gerenciado do libtiff) — ver `THIRD_PARTY_LICENSES.md` para o
texto completo da licença (estilo BSD-3-Clause, compatível com distribuição comercial fechada).
