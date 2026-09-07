namespace SCANOVA.Core.Enums;

/// <summary>Origem de digitalização de um scanner WIA.</summary>
public enum ScanSource
{
    Automatic,
    Flatbed,
    Feeder,

    /// <summary>Alimentador com digitalização automática de frente e verso.</summary>
    FeederDuplex,
}
