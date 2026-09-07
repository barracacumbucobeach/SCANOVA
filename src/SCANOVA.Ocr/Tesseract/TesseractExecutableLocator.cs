using System.Runtime.InteropServices;

namespace SCANOVA.Ocr.Tesseract;

/// <summary>
/// Localiza o executável do Tesseract trazido pelo pacote NAPS2.Tesseract.Binaries: os binários
/// de cada plataforma são copiados, pelo próprio pacote, para uma subpasta do diretório de saída
/// (<c>_win64</c>/<c>_win32</c>/<c>_winarm</c>/<c>_linux</c>/<c>_linuxarm</c>/<c>_mac</c>/<c>_macarm</c>).
/// </summary>
internal static class TesseractExecutableLocator
{
    public static string Locate()
    {
        var (subfolder, exeName) = GetPlatformFolder();
        var path = Path.Combine(AppContext.BaseDirectory, subfolder, exeName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Executável do Tesseract não encontrado em \"{path}\". Verifique se o pacote NAPS2.Tesseract.Binaries foi restaurado corretamente.",
                path);
        }

        return path;
    }

    private static (string Subfolder, string ExeName) GetPlatformFolder()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => ("_winarm", "tesseract.exe"),
                Architecture.X86 => ("_win32", "tesseract.exe"),
                _ => ("_win64", "tesseract.exe"),
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? ("_macarm", "tesseract") : ("_mac", "tesseract");
        }

        return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? ("_linuxarm", "tesseract") : ("_linux", "tesseract");
    }
}
