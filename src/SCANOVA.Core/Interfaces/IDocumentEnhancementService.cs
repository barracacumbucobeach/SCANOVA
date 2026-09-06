using SCANOVA.Core.Models;

namespace SCANOVA.Core.Interfaces;

/// <summary>
/// Motor de melhoria de documentos: brilho, contraste, gamma, nitidez, escala de cinza, remoção
/// de fundo, redução de ruído, binarização (global/adaptativa), remoção de pequenas manchas,
/// deskew e correção de perspectiva (seção 19).
/// </summary>
public interface IDocumentEnhancementService
{
    /// <summary>Aplica os ajustes informados, retornando uma nova imagem (não modifica <paramref name="image"/> — edição não destrutiva, seção 45).</summary>
    RasterImage Apply(RasterImage image, ImageAdjustments adjustments);

    /// <summary>Resolve um preset de legibilidade (seção 20) em ajustes concretos.</summary>
    ImageAdjustments ResolvePreset(Enums.EnhancementPreset preset);

    /// <summary>Executa o pipeline "Melhorar automaticamente" (seção 20) de ponta a ponta.</summary>
    RasterImage AutoEnhance(RasterImage image, Enums.EnhancementPreset preset = Enums.EnhancementPreset.Normal);
}
