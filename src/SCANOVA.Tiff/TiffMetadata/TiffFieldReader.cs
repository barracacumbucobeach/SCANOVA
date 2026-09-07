using BitMiracle.LibTiff.Classic;
using TiffFile = BitMiracle.LibTiff.Classic.Tiff;

namespace SCANOVA.Tiff.TiffMetadata;

/// <summary>
/// Pequenos helpers para ler tags TIFF de forma segura (retornando valores padrão quando
/// ausentes). Usa o alias <c>TiffFile</c> para <c>BitMiracle.LibTiff.Classic.Tiff</c> em todo o
/// projeto: o nome do namespace deste projeto (<c>SCANOVA.Tiff</c>) colide textualmente com o
/// nome curto da classe da biblioteca (<c>Tiff</c>), o que causa ambiguidade de compilação sem
/// o alias.
/// </summary>
internal static class TiffFieldReader
{
    public static int? GetInt(TiffFile tiff, TiffTag tag)
    {
        var value = tiff.GetField(tag);
        return value is { Length: > 0 } ? value[0].ToInt() : null;
    }

    public static double? GetDouble(TiffFile tiff, TiffTag tag)
    {
        var value = tiff.GetField(tag);
        return value is { Length: > 0 } ? value[0].ToDouble() : null;
    }
}
