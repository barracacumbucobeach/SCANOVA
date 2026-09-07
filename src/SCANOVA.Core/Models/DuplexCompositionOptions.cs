namespace SCANOVA.Core.Models;

/// <summary>
/// Opções de composição de páginas de frente e verso escaneadas em duas passagens separadas
/// (scanner sem alimentador com duplex automático — ver <c>ScanSource.FeederDuplex</c> para o
/// caso em que o próprio hardware já faz isso). O comportamento correto depende do mecanismo
/// físico do alimentador, então nada aqui é adivinhado automaticamente — a interface deixa o
/// usuário confirmar visualmente antes de salvar (seção 45 — nunca operação destrutiva/silenciosa).
/// </summary>
public sealed record DuplexCompositionOptions
{
    /// <summary>
    /// Quando verdadeiro, a lista de versos é usada de trás para frente antes de intercalar.
    /// Necessário no fluxo de duplex manual mais comum: escanear a pilha de frentes (páginas
    /// saem na ordem 1, 2, 3...), virar a pilha inteira de uma vez (sem reordenar folha por
    /// folha) e escanear de novo — os versos saem na ordem inversa (verso de N primeiro).
    /// Falso (padrão) assume que os versos já foram escaneados na mesma ordem das frentes.
    /// </summary>
    public bool ReverseBackOrder { get; init; }

    /// <summary>
    /// Quando verdadeiro, cada página de verso é girada 180° antes de compor — necessário
    /// quando o alimentador entrega o verso invertido de cabeça para baixo (depende do
    /// mecanismo físico do scanner).
    /// </summary>
    public bool RotateBackPages180 { get; init; }

    public static DuplexCompositionOptions Default { get; } = new();
}
