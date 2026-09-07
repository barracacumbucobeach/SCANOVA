namespace SCANOVA.Core.Enums;

/// <summary>Presets do modo "Melhorar legibilidade", apresentados em linguagem simples ao usuário.</summary>
public enum EnhancementPreset
{
    /// <summary>Para documentos já bons — ajustes leves.</summary>
    Light,

    /// <summary>Para a maioria dos documentos.</summary>
    Normal,

    /// <summary>Para documentos apagados/baixo contraste. Requer validação visual do usuário.</summary>
    Strong,

    /// <summary>Controles manuais (modo avançado).</summary>
    Custom,
}
