using SCANOVA.Core.Models;
using SkiaSharp;
using CorePixelFormat = SCANOVA.Core.Enums.PixelFormat;

namespace SCANOVA.Ocr.Tests;

/// <summary>
/// Gera imagens sintéticas de "documento escaneado" com texto real desenhado por código (nunca
/// um documento de uma pessoa real — seção 122), usando a fonte embutida do projeto (via
/// SkiaSharp, carregada diretamente do arquivo) em vez de qualquer fonte do sistema — o mesmo
/// texto de teste deve ficar legível para o Tesseract independentemente do que estiver instalado
/// na máquina que executa os testes.
/// </summary>
internal static class TestImages
{
    private static readonly string FontPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "NotoSans.ttf");

    /// <summary>Página branca com uma ou mais linhas de texto preto, desenhadas com a fonte embutida.</summary>
    public static RasterImage CreateTextPage(int width, int height, params string[] lines)
    {
        using var typeface = SKTypeface.FromFile(FontPath)
            ?? throw new InvalidOperationException($"Não foi possível carregar a fonte de teste em \"{FontPath}\".");

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var font = new SKFont(typeface, size: 36);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        var y = 60f;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 40, y, font, paint);
            y += 60f;
        }

        canvas.Flush();
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);

        var stride = bitmap.RowBytes;
        var pixels = new byte[stride * bitmap.Height];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

        // 300 DPI: texto do tamanho usado aqui (fonte de 36px) fica em uma faixa de altura
        // confortável para o Tesseract reconhecer com boa confiança.
        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 300, 300);
    }

    public static RasterImage CreateBlankPage(int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        Array.Fill(pixels, (byte)255);
        return new RasterImage(width, height, stride, CorePixelFormat.Rgba32, pixels, 300, 300);
    }
}
