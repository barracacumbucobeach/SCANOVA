namespace SCANOVA.Core.Models;

/// <summary>Ponto 2D em coordenadas de pixel, usado para regiões de corte com perspectiva.</summary>
public readonly record struct DocumentPoint(double X, double Y);

/// <summary>
/// Região de corte de um documento. Quando os quatro cantos (<see cref="TopLeft"/>...<see cref="BottomLeft"/>)
/// não formam um retângulo alinhado aos eixos, a correção de perspectiva deve ser aplicada antes do corte.
/// </summary>
public sealed class CropRegion
{
    public required DocumentPoint TopLeft { get; init; }
    public required DocumentPoint TopRight { get; init; }
    public required DocumentPoint BottomRight { get; init; }
    public required DocumentPoint BottomLeft { get; init; }

    /// <summary>Cria uma região retangular simples, alinhada aos eixos.</summary>
    public static CropRegion FromRectangle(double x, double y, double width, double height) => new()
    {
        TopLeft = new DocumentPoint(x, y),
        TopRight = new DocumentPoint(x + width, y),
        BottomRight = new DocumentPoint(x + width, y + height),
        BottomLeft = new DocumentPoint(x, y + height),
    };

    /// <summary>Indica se a região é um retângulo alinhado aos eixos (não requer correção de perspectiva).</summary>
    public bool IsAxisAlignedRectangle =>
        TopLeft.Y == TopRight.Y && BottomLeft.Y == BottomRight.Y &&
        TopLeft.X == BottomLeft.X && TopRight.X == BottomRight.X;
}
