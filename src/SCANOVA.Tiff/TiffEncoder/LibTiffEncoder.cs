using BitMiracle.LibTiff.Classic;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Tiff.TiffMetadata;
using TiffFile = BitMiracle.LibTiff.Classic.Tiff;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Tiff.TiffEncoder;

/// <summary>
/// Implementação de <see cref="ITiffEncoder"/> usando BitMiracle.LibTiff.NET — o requisito
/// crítico do produto (seção 22/23/27): TIFF 200 DPI, 1 bit, CCITT Group 4. Nunca simplesmente
/// renomeia um arquivo; a imagem precisa já estar no formato de pixel correspondente ao
/// <see cref="ColorMode"/> pedido (a conversão grayscale→binarização fica no pipeline de mais
/// alto nível, <see cref="TiffDocumentPipeline"/>).
/// </summary>
public sealed class LibTiffEncoder : ITiffEncoder
{
    public Task EncodeAsync(RasterImage image, TiffSettings settings, string filePath, CancellationToken cancellationToken = default) =>
        EncodeMultiPageAsync(new[] { image }, settings, filePath, cancellationToken);

    public Task EncodeMultiPageAsync(IReadOnlyList<RasterImage> pages, TiffSettings settings, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count == 0)
        {
            throw new TiffEncodingException(
                "Não há páginas para salvar no arquivo TIFF.",
                "EncodeMultiPageAsync chamado com lista de páginas vazia.");
        }

        foreach (var page in pages)
        {
            ValidatePageAgainstSettings(page, settings);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Seção 60: nunca deixar uma exceção técnica de framework (caminho inválido,
                // permissão negada, etc.) vazar crua para fora do serviço.
                throw new FileAccessException(
                    "Não foi possível salvar o arquivo. Verifique se o caminho e a pasta de destino são válidos.",
                    $"Falha ao criar o diretório de destino para {filePath}: {ex.Message}", ex);
            }

            TiffFile tiff;
            try
            {
                tiff = TiffFile.Open(filePath, "w")
                       ?? throw new TiffEncodingException(
                           "Não foi possível criar o arquivo TIFF.",
                           $"Tiff.Open('w') retornou null para {filePath}.");
            }
            catch (TiffEncodingException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FileAccessException(
                    "Não foi possível criar o arquivo TIFF. Verifique se a pasta está disponível e tente novamente.",
                    $"Tiff.Open('w') falhou para {filePath}: {ex.Message}", ex);
            }

            using (tiff)
            {
                for (var i = 0; i < pages.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        WritePage(tiff, pages[i], settings);
                    }
                    catch (ScanovaException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new TiffEncodingException(
                            "Não foi possível gravar o conteúdo no arquivo TIFF.",
                            $"Falha ao escrever a página {i}: {ex.Message}", ex);
                    }

                    if (!tiff.WriteDirectory())
                    {
                        throw new TiffEncodingException(
                            "Não foi possível finalizar uma página do arquivo TIFF.",
                            $"WriteDirectory retornou false na página {i}.");
                    }
                }
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<RasterImage>> DecodeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new ImageLoadException(
                "O arquivo não foi encontrado.",
                $"Arquivo TIFF inexistente: {filePath}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            TiffFile tiff;
            try
            {
                tiff = TiffFile.Open(filePath, "r")
                       ?? throw new ImageLoadException(
                           "O arquivo não é um TIFF válido ou está corrompido.",
                           $"Tiff.Open('r') retornou null para {filePath}.");
            }
            catch (ImageLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ImageLoadException(
                    "Não foi possível abrir o arquivo TIFF. Ele pode estar corrompido.",
                    $"Tiff.Open('r') falhou para {filePath}: {ex.Message}", ex);
            }

            using (tiff)
            {
                var pages = new List<RasterImage>();
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pages.Add(DecodePage(tiff));
                }
                while (tiff.ReadDirectory());

                return (IReadOnlyList<RasterImage>)pages;
            }
        }, cancellationToken);
    }

    private static void ValidatePageAgainstSettings(RasterImage image, TiffSettings settings)
    {
        switch (settings.ColorMode)
        {
            case ColorMode.BlackAndWhite1Bit when image.Format != CorePixelFormat.Bilevel1:
                throw new TiffEncodingException(
                    "A imagem precisa estar em preto e branco (1 bit) para este modo de TIFF.",
                    $"ColorMode=BlackAndWhite1Bit exige {CorePixelFormat.Bilevel1}, imagem está em {image.Format}.");

            case ColorMode.Grayscale when image.Format != CorePixelFormat.Gray8:
                throw new TiffEncodingException(
                    "A imagem precisa estar em escala de cinza para este modo de TIFF.",
                    $"ColorMode=Grayscale exige {CorePixelFormat.Gray8}, imagem está em {image.Format}.");

            case ColorMode.Color when image.Format is not (CorePixelFormat.Rgb24 or CorePixelFormat.Rgba32):
                throw new TiffEncodingException(
                    "A imagem precisa estar em cor para este modo de TIFF.",
                    $"ColorMode=Color exige Rgb24/Rgba32, imagem está em {image.Format}.");
        }

        if (settings.Compression == CompressionType.CcittGroup4 && image.Format != CorePixelFormat.Bilevel1)
        {
            // Seção 22/23: CCITT Group 4 é compressão para imagem bilevel — nunca gerar um TIFF
            // "documental" fora de 1 bit, mesmo que o chamador tenha pedido.
            throw new TiffEncodingException(
                "CCITT Group 4 exige uma imagem em preto e branco (1 bit). Binarize a imagem antes de salvar.",
                $"Compression=CcittGroup4 mas a imagem está em {image.Format}.");
        }
    }

    private static void WritePage(TiffFile tiff, RasterImage image, TiffSettings settings)
    {
        tiff.SetField(TiffTag.IMAGEWIDTH, image.Width);
        tiff.SetField(TiffTag.IMAGELENGTH, image.Height);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.XRESOLUTION, settings.HorizontalDpi);
        tiff.SetField(TiffTag.YRESOLUTION, settings.VerticalDpi);
        tiff.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
        tiff.SetField(TiffTag.SOFTWARE, "SCANOVA");
        tiff.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
        tiff.SetField(TiffTag.COMPRESSION, ToLibTiffCompression(settings.Compression));

        switch (image.Format)
        {
            case CorePixelFormat.Bilevel1:
                tiff.SetField(TiffTag.BITSPERSAMPLE, 1);
                tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                // Convenção do RasterImage (bit 1 = preto) corresponde exatamente a MinIsWhite
                // (valor de amostra 0 = branco), a interpretação fotométrica convencional para
                // imagens estilo fax/CCITT — ver seção 25.
                tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
                // CCITT Group 3/4 codifica a imagem inteira como uma unidade 2D — uma única
                // strip evita qualquer ambiguidade de corte de linhas entre strips.
                tiff.SetField(TiffTag.ROWSPERSTRIP, image.Height);
                WriteScanlines(tiff, image);
                break;

            case CorePixelFormat.Gray8:
                tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
                tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                tiff.SetField(TiffTag.ROWSPERSTRIP, tiff.DefaultStripSize(0));
                WriteScanlines(tiff, image);
                break;

            case CorePixelFormat.Rgb24:
                tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
                tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
                tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
                tiff.SetField(TiffTag.ROWSPERSTRIP, tiff.DefaultStripSize(0));
                WriteScanlines(tiff, image);
                break;

            case CorePixelFormat.Rgba32:
                // Simplificação deliberada: TIFF colorido aqui não carrega canal alfa (a tag
                // EXTRASAMPLES não é necessária para o caso de uso do produto — "PDF/TIFF
                // Colorido" é sempre opaco). Achata para RGB24 antes de gravar.
                tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
                tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
                tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
                tiff.SetField(TiffTag.ROWSPERSTRIP, tiff.DefaultStripSize(0));
                WriteScanlinesRgbaAsRgb(tiff, image);
                break;

            default:
                throw new TiffEncodingException(
                    "Este formato de imagem não é suportado para exportação em TIFF.",
                    $"WritePage: formato não tratado {image.Format}.");
        }
    }

    private static Compression ToLibTiffCompression(CompressionType compression) => compression switch
    {
        CompressionType.None => Compression.NONE,
        CompressionType.CcittGroup4 => Compression.CCITTFAX4,
        CompressionType.CcittGroup3 => Compression.CCITTFAX3,
        CompressionType.Lzw => Compression.LZW,
        CompressionType.Deflate => Compression.DEFLATE,
        CompressionType.Jpeg => Compression.JPEG,
        _ => throw new ArgumentOutOfRangeException(nameof(compression), compression, null),
    };

    private static void WriteScanlines(TiffFile tiff, RasterImage image)
    {
        var rowBuffer = new byte[image.Stride];
        for (var row = 0; row < image.Height; row++)
        {
            Array.Copy(image.Pixels, row * image.Stride, rowBuffer, 0, image.Stride);
            if (!tiff.WriteScanline(rowBuffer, row))
            {
                throw new TiffEncodingException(
                    "Não foi possível gravar o conteúdo da imagem no arquivo TIFF.",
                    $"WriteScanline retornou false na linha {row}.");
            }
        }
    }

    private static void WriteScanlinesRgbaAsRgb(TiffFile tiff, RasterImage image)
    {
        var rowBuffer = new byte[image.Width * 3];
        for (var row = 0; row < image.Height; row++)
        {
            var srcRow = row * image.Stride;
            for (var x = 0; x < image.Width; x++)
            {
                var s = srcRow + x * 4;
                var d = x * 3;
                rowBuffer[d] = image.Pixels[s];
                rowBuffer[d + 1] = image.Pixels[s + 1];
                rowBuffer[d + 2] = image.Pixels[s + 2];
            }

            if (!tiff.WriteScanline(rowBuffer, row))
            {
                throw new TiffEncodingException(
                    "Não foi possível gravar o conteúdo da imagem no arquivo TIFF.",
                    $"WriteScanline retornou false na linha {row}.");
            }
        }
    }

    private static RasterImage DecodePage(TiffFile tiff)
    {
        var width = TiffFieldReader.GetInt(tiff, TiffTag.IMAGEWIDTH)
                    ?? throw new ImageLoadException("O arquivo TIFF está corrompido (largura ausente).", "IMAGEWIDTH ausente.");
        var height = TiffFieldReader.GetInt(tiff, TiffTag.IMAGELENGTH)
                     ?? throw new ImageLoadException("O arquivo TIFF está corrompido (altura ausente).", "IMAGELENGTH ausente.");
        var bitsPerSample = TiffFieldReader.GetInt(tiff, TiffTag.BITSPERSAMPLE) ?? 1;
        var samplesPerPixel = TiffFieldReader.GetInt(tiff, TiffTag.SAMPLESPERPIXEL) ?? 1;
        var photometricRaw = TiffFieldReader.GetInt(tiff, TiffTag.PHOTOMETRIC);
        var photometric = photometricRaw.HasValue ? (Photometric)photometricRaw.Value : Photometric.MINISBLACK;
        var xres = TiffFieldReader.GetDouble(tiff, TiffTag.XRESOLUTION) ?? 200;
        var yres = TiffFieldReader.GetDouble(tiff, TiffTag.YRESOLUTION) ?? 200;

        if (bitsPerSample == 1 && samplesPerPixel == 1)
        {
            return DecodeBilevel(tiff, width, height, photometric, xres, yres);
        }

        if (bitsPerSample == 8 && samplesPerPixel == 1)
        {
            return DecodeGray8(tiff, width, height, photometric, xres, yres);
        }

        if (bitsPerSample == 8 && samplesPerPixel == 3)
        {
            return DecodeRgb24(tiff, width, height, xres, yres);
        }

        // Formatos não tratados explicitamente (paletizado, YCbCr/JPEG, 16-bit por amostra,
        // RGBA com canal alfa, etc.) — decodifica via a API de conveniência RGBA do libtiff em
        // vez de falhar. Menos fiel à profundidade de bits original, mas garante que o arquivo
        // ao menos abra (seção 28).
        return DecodeViaRgbaFallback(tiff, width, height, xres, yres);
    }

    private static RasterImage DecodeBilevel(TiffFile tiff, int width, int height, Photometric photometric, double xres, double yres)
    {
        var stride = RasterImage.MinimumStride(width, CorePixelFormat.Bilevel1);
        var pixels = new byte[stride * height];
        ReadScanlinesInto(tiff, pixels, stride, height);

        // Nossa convenção (PixelFormat.Bilevel1): bit 1 = preto. MinIsWhite já corresponde a
        // isso; MinIsBlack é o inverso e precisa ser invertido na leitura.
        if (photometric == Photometric.MINISBLACK)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (byte)~pixels[i];
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Bilevel1, pixels, xres, yres);
    }

    private static RasterImage DecodeGray8(TiffFile tiff, int width, int height, Photometric photometric, double xres, double yres)
    {
        var stride = width;
        var pixels = new byte[stride * height];
        ReadScanlinesInto(tiff, pixels, stride, height);

        if (photometric == Photometric.MINISWHITE)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (byte)(255 - pixels[i]);
            }
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Gray8, pixels, xres, yres);
    }

    private static RasterImage DecodeRgb24(TiffFile tiff, int width, int height, double xres, double yres)
    {
        var stride = width * 3;
        var pixels = new byte[stride * height];
        ReadScanlinesInto(tiff, pixels, stride, height);
        return new RasterImage(width, height, stride, CorePixelFormat.Rgb24, pixels, xres, yres);
    }

    private static void ReadScanlinesInto(TiffFile tiff, byte[] destination, int stride, int height)
    {
        var scanlineSize = tiff.ScanlineSize();
        var rowBuffer = new byte[Math.Max(scanlineSize, stride)];

        for (var row = 0; row < height; row++)
        {
            if (!tiff.ReadScanline(rowBuffer, row))
            {
                throw new ImageLoadException(
                    "Não foi possível ler o conteúdo do arquivo TIFF. Ele pode estar corrompido.",
                    $"ReadScanline retornou false na linha {row}.");
            }

            Array.Copy(rowBuffer, 0, destination, row * stride, Math.Min(scanlineSize, stride));
        }
    }

    private static RasterImage DecodeViaRgbaFallback(TiffFile tiff, int width, int height, double xres, double yres)
    {
        var raster = new int[width * height];
        if (!tiff.ReadRGBAImage(width, height, raster))
        {
            throw new ImageLoadException(
                "Não foi possível decodificar o conteúdo do arquivo TIFF.",
                "ReadRGBAImage retornou false.");
        }

        // TIFFReadRGBAImage retorna as linhas de baixo para cima (convenção clássica do
        // libtiff); inverte para a convenção top-down usada por RasterImage.
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            var sourceRow = height - 1 - row;
            Buffer.BlockCopy(raster, sourceRow * stride, pixels, row * stride, stride);
        }

        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, xres, yres);
    }
}
