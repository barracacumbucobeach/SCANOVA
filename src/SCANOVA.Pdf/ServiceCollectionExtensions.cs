using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using SCANOVA.Core.Interfaces;
using SCANOVA.Pdf.PdfRasterizer;
using SCANOVA.Pdf.PdfWriter;

namespace SCANOVA.Pdf;

/// <summary>Registro de injeção de dependência dos serviços de PDF (PDFsharp/PDFtoImage/PdfPig).</summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScanovaPdf(this IServiceCollection services)
    {
        services.AddSingleton<IPdfService, PdfSharpPdfService>();
        services.AddSingleton<IPdfRasterizer, PdfToImagePdfRasterizer>();
        return services;
    }
}
