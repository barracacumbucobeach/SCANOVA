namespace SCANOVA.Core.Enums;

/// <summary>Estratégia de corte de um documento digitalizado/aberto.</summary>
public enum CropMode
{
    /// <summary>Mantém a imagem original, sem cortar.</summary>
    None,

    /// <summary>Corte manual definido pelo usuário (retângulo/quadrilátero).</summary>
    Manual,

    /// <summary>Corte automático a partir da detecção de documento, sem perguntar ao usuário.</summary>
    Automatic,

    /// <summary>Corta automaticamente apenas quando a confiança da detecção ultrapassa o limite configurado; caso contrário pergunta ao usuário.</summary>
    ThresholdedByConfidence,
}
