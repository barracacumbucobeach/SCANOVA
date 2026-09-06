namespace SCANOVA.Core.Models;

/// <summary>Documento aberto/em edição no SCANOVA, composto por uma ou mais páginas.</summary>
public sealed class Document
{
    public required Guid Id { get; init; }

    /// <summary>Caminho do arquivo original em disco, quando o documento foi aberto de um arquivo. Nulo quando recém-digitalizado.</summary>
    public string? SourcePath { get; init; }

    public required List<DocumentPage> Pages { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DocumentPage? GetPage(int index) => Pages.Count > index ? Pages[index] : null;
}
