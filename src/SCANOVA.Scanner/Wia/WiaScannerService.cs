using System.Runtime.Versioning;
using NAPS2.Wia;
using SCANOVA.Core.Enums;
using SCANOVA.Core.Exceptions;
using SCANOVA.Core.Interfaces;
using SCANOVA.Core.Models;

namespace SCANOVA.Scanner.Wia;

/// <summary>
/// Implementação de <see cref="IScannerService"/> usando Windows Image Acquisition (seção 9),
/// via a biblioteca <c>NAPS2.Wia</c> (MIT, ver THIRD_PARTY_LICENSES.md) — um wrapper de baixo
/// nível para WIA 1.0/2.0 mantido pelo projeto NAPS2, escolhido em vez de COM tardio
/// (late-bound) porque expõe as constantes de propriedade/erro do WIA como código real e
/// verificável, em vez de strings/GUIDs mágicos que não haveria como confirmar sem uma máquina
/// Windows com scanner físico disponível (seção 138 — "não inventar APIs").
///
/// Só funciona no Windows: em qualquer outra plataforma, <see cref="NativeWiaObject"/> lança
/// <see cref="InvalidOperationException"/> ao tentar determinar a versão do WIA — capturado
/// abaixo e tratado como "nenhum scanner disponível" (não como erro), compatível com o fluxo da
/// seção 9.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WiaScannerService : IScannerService
{
    private readonly IImageLoader _imageLoader;

    public WiaScannerService(IImageLoader imageLoader)
    {
        _imageLoader = imageLoader;
    }

    public Task<IReadOnlyList<ScannerInfo>> DiscoverScannersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            var scanners = new List<ScannerInfo>();

            using var deviceManager = TryCreateDeviceManager();
            if (deviceManager is null)
            {
                return (IReadOnlyList<ScannerInfo>)scanners;
            }

            IEnumerable<WiaDeviceInfo> deviceInfos;
            try
            {
                deviceInfos = deviceManager.GetDeviceInfos();
            }
            catch (WiaException)
            {
                return (IReadOnlyList<ScannerInfo>)scanners;
            }

            var isFirst = true;
            foreach (var info in deviceInfos)
            {
                using (info)
                {
                    try
                    {
                        scanners.Add(new ScannerInfo
                        {
                            Id = info.Id(),
                            Name = info.Name(),
                            Manufacturer = info.Properties.GetOrNull(WiaPropertyId.DIP_VEND_DESC)?.Value as string,
                            IsDefault = isFirst,
                        });
                        isFirst = false;
                    }
                    catch (WiaException)
                    {
                        // Um dispositivo com propriedades inacessíveis não deve impedir a
                        // listagem dos demais.
                    }
                }
            }

            return (IReadOnlyList<ScannerInfo>)scanners;
        }, cancellationToken);
    }

    public Task<RasterImage> ScanAsync(ScanSettings settings, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(async () =>
        {
            using var deviceManager = TryCreateDeviceManager()
                ?? throw new ScannerException(
                    "Nenhum scanner foi encontrado. Verifique se o scanner está conectado e instalado no Windows.",
                    "WiaDeviceManager indisponível (WIA não suportado nesta plataforma ou não instalado).");

            WiaDevice device;
            try
            {
                device = deviceManager.FindDevice(settings.ScannerId);
            }
            catch (WiaException ex)
            {
                throw new ScannerException(
                    MapWiaErrorToUserMessage(ex.ErrorCode, "Não foi possível conectar ao scanner selecionado."),
                    $"FindDevice falhou: {ex.Message} (0x{ex.ErrorCode:X})", ex);
            }

            using (device)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.05);

                using var item = ResolveScanItem(device, settings.Source)
                    ?? throw new ScannerException(
                        "Não foi possível iniciar a digitalização. O scanner não retornou nenhuma origem de digitalização.",
                        "Nenhum sub-item (Flatbed/Feeder) encontrado no dispositivo.");

                ApplyScanSettings(item, settings);
                cancellationToken.ThrowIfCancellationRequested();

                using var transfer = item.StartTransfer();
                using var cancelRegistration = cancellationToken.Register(transfer.Cancel);

                Stream? scannedStream = null;
                transfer.Progress += (_, args) => progress?.Report(0.1 + (args.Percent / 100.0 * 0.8));
                transfer.PageScanned += (_, args) => scannedStream = args.Stream;

                bool completed;
                try
                {
                    completed = transfer.Download();
                }
                catch (WiaException ex)
                {
                    throw new ScannerException(
                        MapWiaErrorToUserMessage(ex.ErrorCode, "Não foi possível concluir a digitalização. Verifique o scanner e tente novamente."),
                        $"WiaTransfer.Download falhou: {ex.Message} (0x{ex.ErrorCode:X})", ex);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!completed)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (scannedStream is null)
                {
                    throw new ScannerException(
                        "A digitalização não retornou nenhuma imagem.",
                        "WiaTransfer.Download() concluiu sem disparar PageScanned.");
                }

                using (scannedStream)
                {
                    progress?.Report(0.95);
                    var image = await _imageLoader.LoadAsync(scannedStream, cancellationToken).ConfigureAwait(false);
                    progress?.Report(1.0);
                    return image;
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Cria o <see cref="WiaDeviceManager"/>, tratando indisponibilidade da plataforma (não é
    /// Windows, WIA não instalado) como <c>null</c> em vez de deixar a exceção escapar.
    /// </summary>
    private static WiaDeviceManager? TryCreateDeviceManager()
    {
        try
        {
            return new WiaDeviceManager();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Seleciona o item de digitalização (seção 9 — "Origem"). WIA nomeia os itens-raiz de um
    /// dispositivo de forma padronizada como "Flatbed" e/ou "Feeder" — confirmado em código real
    /// (não documentação inferida): https://github.com/cyanfish/naps2/blob/master/NAPS2.Sdk/Scan/Internal/Wia/WiaScanDriver.cs
    /// </summary>
    private static WiaItem? ResolveScanItem(WiaDevice device, ScanSource source) => source switch
    {
        ScanSource.Flatbed => device.FindSubItem("Flatbed") ?? device.GetSubItems().FirstOrDefault(),
        ScanSource.Feeder or ScanSource.FeederDuplex => device.FindSubItem("Feeder") ?? device.FindSubItem("Flatbed"),
        _ => device.FindSubItem("Feeder") ?? device.FindSubItem("Flatbed") ?? device.GetSubItems().FirstOrDefault(),
    };

    private static void ApplyScanSettings(WiaItem item, ScanSettings settings)
    {
        // Valores de WIA_DATA_TYPE confirmados em uso real (não documentação inferida):
        // https://github.com/cyanfish/naps2/blob/master/NAPS2.Sdk/Scan/Internal/Wia/WiaScanDriver.cs
        var dataType = settings.ColorMode switch
        {
            ColorMode.Color => 3,
            ColorMode.Grayscale => 2,
            ColorMode.BlackAndWhite1Bit => 0,
            _ => 3,
        };
        item.SetProperty(WiaPropertyId.IPA_DATATYPE, dataType);

        var xRes = (int)settings.Dpi;
        var yRes = (int)settings.Dpi;
        // SetPropertyClosest ajusta para o DPI suportado mais próximo quando o dispositivo não
        // aceita o valor exato solicitado, em vez de falhar.
        item.SetPropertyClosest(WiaPropertyId.IPS_XRES, ref xRes);
        item.SetPropertyClosest(WiaPropertyId.IPS_YRES, ref yRes);

        if (settings.Source == ScanSource.FeederDuplex)
        {
            item.SetProperty(WiaPropertyId.IPS_DOCUMENT_HANDLING_SELECT, WiaPropertyValue.DUPLEX);
        }
    }

    /// <summary>Traduz códigos de erro WIA conhecidos (seção 77: scanner desconectado/ocupado/etc.) em mensagens amigáveis (seção 60).</summary>
    private static string MapWiaErrorToUserMessage(uint errorCode, string fallback) => errorCode switch
    {
        WiaErrorCodes.OFFLINE or WiaErrorCodes.NO_DEVICE_AVAILABLE =>
            "O scanner não está conectado ou foi desligado. Verifique a conexão e tente novamente.",
        WiaErrorCodes.BUSY =>
            "O scanner está ocupado com outra digitalização. Aguarde e tente novamente.",
        WiaErrorCodes.PAPER_EMPTY =>
            "Não há papel no alimentador do scanner.",
        WiaErrorCodes.PAPER_JAM =>
            "Há um papel preso no scanner. Verifique o alimentador.",
        WiaErrorCodes.COVER_OPEN =>
            "A tampa do scanner está aberta.",
        WiaErrorCodes.WARMING_UP =>
            "O scanner ainda está se preparando. Aguarde alguns segundos e tente novamente.",
        WiaErrorCodes.LOCKED =>
            "O scanner está bloqueado. Verifique o equipamento.",
        WiaErrorCodes.COMMUNICATION =>
            "Falha de comunicação com o scanner. Verifique o cabo/conexão e tente novamente.",
        _ => fallback,
    };
}
