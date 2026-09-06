namespace SCANOVA.Core.Enums;

/// <summary>
/// Tamanho físico estimado/selecionado de um documento. Usado para normalizar dimensão
/// em pixels a partir do DPI (pixels = polegadas × DPI), nunca para classificar
/// juridicamente o conteúdo do documento.
/// </summary>
public enum DocumentSize
{
    /// <summary>Detecta automaticamente a partir da imagem digitalizada.</summary>
    Automatic,

    A4,
    A5,

    /// <summary>Documento pequeno (ex.: cartão, documento de identificação) — apenas dimensão física estimada.</summary>
    Small,

    /// <summary>Documento de tamanho médio.</summary>
    Medium,

    /// <summary>Dimensões definidas manualmente pelo usuário.</summary>
    Custom,
}
