using System.Reflection;
using PdfSharp.Fonts;

namespace SCANOVA.Pdf.PdfWriter;

/// <summary>
/// Resolve a única fonte que este projeto precisa (Noto Sans, embutida como recurso — ver
/// <c>Assets/NotoSans.ttf</c> e sua licença em <c>Assets/NotoSans-OFL.txt</c>) para desenhar a
/// camada de texto invisível/pesquisável do OCR (Fase 9).
/// </summary>
/// <remarks>
/// Necessário porque o build "CORE" (sem GDI+/WPF) do PDFsharp — o único que roda fora do
/// Windows — não enumera fontes do sistema operacional; sem um <see cref="IFontResolver"/>
/// registrado, qualquer uso de <c>XFont</c>/<c>XGraphics.DrawString</c> lançaria uma exceção em
/// Linux/macOS. Como a fonte é usada apenas para texto invisível (a aparência visual da página
/// nunca muda), uma única família/estilo é suficiente — não há necessidade de negrito/itálico
/// nem de mapear nomes de família diferentes.
/// </remarks>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string FamilyName = "NotoSansScanova";
    private const string ResourceName = "SCANOVA.Pdf.Assets.NotoSans.ttf";

    private static readonly Lazy<byte[]> FontBytes = new(LoadFontBytes);

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => new(FamilyName);

    public byte[]? GetFont(string faceName) => FontBytes.Value;

    private static byte[] LoadFontBytes()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso embutido \"{ResourceName}\" não encontrado — verifique o <EmbeddedResource> no SCANOVA.Pdf.csproj.");

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>Registra este resolvedor globalmente (idempotente — chamar mais de uma vez não tem efeito colateral).</summary>
    public static void EnsureRegistered()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
        }
    }
}
