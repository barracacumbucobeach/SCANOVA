namespace SCANOVA.Core.Enums;

/// <summary>Estado da licença do produto.</summary>
public enum LicenseStatus
{
    /// <summary>Nenhuma licença ativada — aplicativo em modo demonstração.</summary>
    Unlicensed,

    /// <summary>Licença vitalícia ativada e válida.</summary>
    Licensed,

    /// <summary>Licença presente porém inválida (assinatura não confere, produto/edição incompatível etc.).</summary>
    Invalid,

    /// <summary>Licença revogada pelo fabricante (ex.: fraude, estorno).</summary>
    Revoked,
}
