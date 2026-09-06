using BitMiracle.LibTiff.Classic;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;
using SCANOVA.Tiff.TiffMetadata;
using TiffFile = BitMiracle.LibTiff.Classic.Tiff;

namespace SCANOVA.Tiff.TiffValidator;

/// <summary>
/// Reabre e valida um arquivo TIFF recém-gerado (seção 26): confirma que é um TIFF legítimo e
/// decodificável, e reporta compressão, bits por amostra, resolução e dimensões diretamente das
/// tags do arquivo (não dos pixels decodificados) — a mesma prova que um leitor TIFF externo
/// enxergaria.
/// </summary>
public sealed class LibTiffValidator : ITiffValidator
{
    public Task<TiffValidationReport> ValidateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return Task.FromResult(new TiffValidationReport
            {
                IsValid = false,
                IsReadableTiff = false,
                IsDecodable = false,
                UserMessage = "Não foi possível validar o arquivo TIFF.",
                TechnicalDetail = $"Arquivo inexistente: {filePath}",
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            TiffFile? tiff = null;
            try
            {
                tiff = TiffFile.Open(filePath, "r");
                if (tiff is null)
                {
                    return new TiffValidationReport
                    {
                        IsValid = false,
                        IsReadableTiff = false,
                        IsDecodable = false,
                        UserMessage = "Não foi possível validar o arquivo TIFF.",
                        TechnicalDetail = "Tiff.Open('r') retornou null — não é um TIFF reconhecível.",
                    };
                }

                var width = TiffFieldReader.GetInt(tiff, TiffTag.IMAGEWIDTH);
                var height = TiffFieldReader.GetInt(tiff, TiffTag.IMAGELENGTH);
                var bitsPerSample = TiffFieldReader.GetInt(tiff, TiffTag.BITSPERSAMPLE);
                var xres = TiffFieldReader.GetDouble(tiff, TiffTag.XRESOLUTION);
                var yres = TiffFieldReader.GetDouble(tiff, TiffTag.YRESOLUTION);
                var compressionRaw = TiffFieldReader.GetInt(tiff, TiffTag.COMPRESSION);
                var compression = compressionRaw.HasValue ? MapCompression((Compression)compressionRaw.Value) : (CompressionType?)null;
                var resUnitRaw = TiffFieldReader.GetInt(tiff, TiffTag.RESOLUTIONUNIT);
                var resolutionUnitIsInch = resUnitRaw.HasValue ? resUnitRaw.Value == (int)ResUnit.INCH : (bool?)null;

                var pageCount = tiff.NumberOfDirectories();

                // "Decodificável": tenta ler ao menos a primeira linha de cada página — prova
                // real de que o conteúdo (não só os cabeçalhos) está íntegro.
                var isDecodable = TryDecodeAllPages(tiff, pageCount);

                var isValid = width is > 0 && height is > 0 && isDecodable;

                return new TiffValidationReport
                {
                    IsValid = isValid,
                    IsReadableTiff = true,
                    IsDecodable = isDecodable,
                    Width = width,
                    Height = height,
                    BitsPerSample = bitsPerSample,
                    HorizontalDpi = xres,
                    VerticalDpi = yres,
                    Compression = compression,
                    ResolutionUnitIsInch = resolutionUnitIsInch,
                    PageCount = pageCount,
                    UserMessage = isValid ? null : "Não foi possível validar completamente o arquivo TIFF.",
                };
            }
            catch (Exception ex)
            {
                return new TiffValidationReport
                {
                    IsValid = false,
                    IsReadableTiff = tiff is not null,
                    IsDecodable = false,
                    UserMessage = "Não foi possível validar o arquivo TIFF.",
                    TechnicalDetail = ex.Message,
                };
            }
            finally
            {
                tiff?.Close();
            }
        }, cancellationToken);
    }

    public async Task<TiffValidationReport> ValidateDocumentalAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var report = await ValidateAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (!report.IsValid)
        {
            return report;
        }

        var satisfiesDocumental =
            report.Is1Bit &&
            report.IsCcittGroup4 &&
            report.MatchesDpi(TiffSettings.Documental.HorizontalDpi) &&
            report.ResolutionUnitIsInch != false; // true ou desconhecido (arquivo de terceiros sem a tag) — só reprova se for explicitamente outra unidade

        if (satisfiesDocumental)
        {
            return report;
        }

        return report with
        {
            IsValid = false,
            UserMessage = "O arquivo TIFF gerado não atende ao preset documental (200 DPI, 1 bit, CCITT Group 4).",
        };
    }

    private static bool TryDecodeAllPages(TiffFile tiff, int pageCount)
    {
        if (pageCount <= 0)
        {
            return false;
        }

        try
        {
            var pageIndex = 0;
            do
            {
                var height = TiffFieldReader.GetInt(tiff, TiffTag.IMAGELENGTH) ?? 0;
                if (height <= 0)
                {
                    return false;
                }

                var scanlineSize = tiff.ScanlineSize();
                if (scanlineSize <= 0)
                {
                    return false;
                }

                var buffer = new byte[scanlineSize];
                if (!tiff.ReadScanline(buffer, 0))
                {
                    return false;
                }

                pageIndex++;
            }
            while (tiff.ReadDirectory());

            return pageIndex == pageCount;
        }
        catch
        {
            return false;
        }
    }

    private static CompressionType MapCompression(Compression compression) => compression switch
    {
        Compression.NONE => CompressionType.None,
        Compression.CCITTFAX4 => CompressionType.CcittGroup4,
        Compression.CCITTFAX3 => CompressionType.CcittGroup3,
        Compression.LZW => CompressionType.Lzw,
        Compression.DEFLATE or Compression.ADOBE_DEFLATE => CompressionType.Deflate,
        Compression.JPEG => CompressionType.Jpeg,
        _ => CompressionType.None,
    };
}
