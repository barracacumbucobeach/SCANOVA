using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Combina páginas de frente e verso escaneadas/abertas separadamente (quando o scanner não tem
/// alimentador com duplex automático) em um único documento multipágina, na ordem correta:
/// frente 1, verso 1, frente 2, verso 2, ... (seção — composição frente/verso).
/// </summary>
public interface IDuplexCompositionService
{
    /// <summary>
    /// Intercala <paramref name="frontPages"/> e <paramref name="backPages"/>. As duas listas
    /// precisam ter o mesmo número de páginas (uma frente para cada verso) e pelo menos uma
    /// página cada — lança <see cref="Exceptions.DuplexCompositionException"/> caso contrário.
    /// Não modifica nenhuma imagem de entrada nem os itens das listas recebidas.
    /// </summary>
    IReadOnlyList<RasterImage> Compose(
        IReadOnlyList<RasterImage> frontPages,
        IReadOnlyList<RasterImage> backPages,
        DuplexCompositionOptions? options = null);
}
