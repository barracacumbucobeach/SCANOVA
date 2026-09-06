# Scanner (WIA)

> **Status:** Fase 4 concluída.

## Abordagem

A primeira implementação usa **WIA (Windows Image Acquisition)**, como pedido pela
especificação (seção 9). `IScannerService` (`SCANOVA.Core.Interfaces`) abstrai a tecnologia de
digitalização, permitindo adicionar outras (ex.: TWAIN/eSCL) no futuro sem alterar a UI.

### Por que `NAPS2.Wia` em vez de COM tardio (`dynamic`)

A forma mais comum de acessar WIA em .NET Core+ é via COM tardio (`Type.GetTypeFromProgID` +
`dynamic`), mas isso exige acertar de cabeça nomes de propriedade e GUIDs do WIA — uma área
notoriamente pouco documentada e fácil de errar, sem qualquer verificação em tempo de
compilação. Sem uma máquina Windows com scanner físico disponível neste ambiente de
desenvolvimento, não haveria como confirmar esses valores por conta própria (contrariando a
seção 138 — "não inventar APIs").

Em vez disso, o SCANOVA usa **[`NAPS2.Wia`](https://github.com/cyanfish/naps2-wia)** (MIT,
mantido pelo projeto [NAPS2](https://github.com/cyanfish/naps2) — um aplicativo de digitalização
open source amplamente usado): um wrapper de baixo nível para WIA 1.0/2.0 que expõe as
constantes de propriedade (`WiaPropertyId`), valores (`WiaPropertyValue`) e códigos de erro
(`WiaErrorCodes`) do WIA como código C# real, testado e mantido — não como strings/GUIDs mágicos
que eu teria que adivinhar. Todos os valores usados por `WiaScannerService` (IDs de propriedade,
nomes de item "Flatbed"/"Feeder", valores de `Data Type`) foram conferidos diretamente no
código-fonte do NAPS2 (`WiaScanDriver.cs`), não inferidos de memória.

## `WiaScannerService`

`SCANOVA.Scanner.Wia.WiaScannerService` implementa `IScannerService`:

- **`DiscoverScannersAsync`**: enumera `WiaDeviceManager.GetDeviceInfos()`, lendo `Name` e
  fabricante de cada dispositivo. Se o WIA estiver indisponível (não é Windows, serviço não
  instalado) — situação real deste ambiente de desenvolvimento Linux — retorna uma **lista
  vazia** em vez de lançar exceção, compatível com o fluxo da seção 9 ("Nenhum scanner foi
  encontrado... [Tentar novamente]").
- **`ScanAsync`**: conecta ao dispositivo pelo `DeviceID`, seleciona o item "Flatbed" ou "Feeder"
  conforme `ScanSettings.Source`, aplica resolução (`IPS_XRES`/`IPS_YRES`, ajustada para o valor
  suportado mais próximo) e modo de cor (`IPA_DATATYPE`: 0=preto e branco, 2=cinza, 3=cor),
  ativa duplex quando `FeederDuplex` é escolhido, e transfere a imagem via `WiaTransfer`. O
  stream retornado é decodificado com `IImageLoader` (reaproveitando o pipeline já testado da
  Fase 2) — nenhum parsing manual de bytes.
- Erros WIA conhecidos (`WiaErrorCodes`) são traduzidos para mensagens amigáveis em português
  (scanner desconectado, ocupado, sem papel, tampa aberta, etc. — seção 60/77).

A classe é marcada `[SupportedOSPlatform("windows")]` — o projeto `SCANOVA.Scanner` continua
`net8.0` puro (compila em Linux/macOS/CI), mas esta classe especificamente só funciona no
Windows; fora dele, `NAPS2.Wia` lança `InvalidOperationException` ao tentar determinar a versão
do WIA, capturada e tratada como "nenhum scanner disponível".

## Perfis de digitalização (seção 11)

`SCANOVA.Core.Models.ScanProfile.Default` — catálogo com os quatro perfis da especificação
(TIFF Documental, PDF Documental, Documento Legível, Colorido), cada um mapeando para um
`ScanSettings` pronto (DPI + modo de cor). A UI (`ScanPage`) mostra apenas o nome e a descrição
em linguagem simples — nunca "DPI" ou "modo de cor" como conceitos separados que o usuário
precisa entender (seção 131).

## Mock para testes

`MockScannerService` existe **apenas** em `tests/SCANOVA.Scanner.Tests` (seção 140 — mocks de
scanner só no ambiente de testes, nunca na aplicação final).

## Limitações conhecidas / próximos passos

- Duplex é configurado via `IPS_DOCUMENT_HANDLING_SELECT` (caminho WIA 2.0). Dispositivos WIA
  1.0 legados usam uma propriedade de nível de dispositivo diferente
  (`DPS_DOCUMENT_HANDLING_SELECT`) — não implementado nesta fase; a maioria dos scanners atuais
  usa WIA 2.0 (padrão desde o Windows Vista).
- **Nenhuma parte desta implementação foi testada contra um scanner físico** — este ambiente de
  desenvolvimento não tem Windows nem hardware de digitalização disponível. A lógica foi escrita
  com base em código-fonte real e testado (NAPS2), não em documentação inferida, mas a
  verificação final em hardware real fica pendente para quando o projeto for aberto/testado no
  Windows.
