namespace SCANOVA.App.Services;

/// <summary>
/// Aplica a preferência de aparência (seção 48/Fase 11 — modo escuro) na janela principal em
/// tempo real, sem precisar reiniciar o aplicativo.
/// </summary>
public interface IThemeService
{
    /// <summary>Aplica a preferência de tema. Valores aceitos: "Light", "Dark", "System" (qualquer outro valor cai em "System").</summary>
    void Apply(string themePreference);
}
