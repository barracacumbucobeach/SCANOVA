# Instalador (Fase 11)

> **Status:** projeto do instalador (MSI, via WiX Toolset) escrito e testado iterativamente contra
> builds reais num runner Windows do GitHub Actions (ver "Como gerar o instalador sem precisar de
> uma máquina Windows própria", abaixo) — vários erros reais de XAML e de autoria WiX já foram
> encontrados e corrigidos dessa forma. Ainda assim, a validação final (rodar o `.msi` de fato
> instalando e abrindo o SCANOVA numa máquina Windows) fica a cargo do usuário, já que este
> ambiente de desenvolvimento não tem acesso a um Windows interativo.

## Decisão: MSI tradicional, não MSIX (seção 91)

O SCANOVA usa um **instalador MSI tradicional** (via WiX Toolset, SDK fixado na versão 5.0.2 —
ver nota sobre licenciamento abaixo), não um pacote MSIX. Motivo:
MSIX pressupõe distribuição pela Microsoft Store (ou, fora dela, exige um certificado de
assinatura de pacote válido, com toda a burocracia de emissão/renovação que isso implica) — o
modelo comercial do SCANOVA é venda direta pelo fabricante (seção 146: licença vitalícia, sem
assinatura, sem intermediário), então um instalador que qualquer cliente possa simplesmente
baixar do site do fabricante e rodar é mais simples e não amarra o produto a nenhuma
infraestrutura de terceiros — o mesmo raciocínio já usado no licenciamento (Fase 10: verificação
100% local, sem servidor de ativação).

O aplicativo continua "Unpackaged" (`WindowsPackageType=None`) — o instalador MSI empacota
diretamente a publicação autocontida (`dotnet publish`) de `SCANOVA.App`, sem envolver o modelo
de pacote MSIX/AppX.

## Estrutura

```text
installer/
├── Build-Installer.ps1                    — publica o app + compila o instalador + gera o checksum
└── SCANOVA.Installer/
    ├── SCANOVA.Installer.wixproj          — projeto WiX (SDK-style), SDK fixado em 5.0.2
    └── Product.wxs                         — definição do pacote MSI
```

- **Instalação por usuário** (`Scope="perUser"` em `Product.wxs`), sem exigir privilégios de
  administrador — mesmo princípio de `app.manifest` (`asInvoker`) do próprio `SCANOVA.App`
  (seção 91).
- **Pasta de instalação:** `%LOCALAPPDATA%\SCANOVA` (consistente com o restante do aplicativo,
  que já usa `%LOCALAPPDATA%\SCANOVA` para configurações/logs/licença — `AppPaths.RootFolder`).
  A licença ativada (Fase 10) e as configurações do usuário nunca ficam dentro da pasta de
  instalação, então desinstalar/reinstalar o SCANOVA nunca desativa a licença.
- **Atalho no menu Iniciar**, criado/removido automaticamente pelo instalador/desinstalador.
- **Arquivos do aplicativo colhidos automaticamente** (elemento `<Files Include="...**" />` do
  WiX, dentro de um `<ComponentGroup Directory="INSTALLFOLDER">` em `Product.wxs`) a partir da
  pasta publicada — nunca listados à mão, para nunca ficar desatualizado conforme o conteúdo
  publicado mudar entre versões. Esse elemento só existe a partir do WiX v5 (motivo pelo qual o
  SDK está fixado em 5.0.2, e não em uma versão 4.x mais antiga — ver `SCANOVA.Installer.wixproj`).
- **Upgrade automático:** instalar uma versão mais nova sobre uma mais antiga substitui os
  arquivos automaticamente (`MajorUpgrade` em `Product.wxs`); a instalação de uma versão mais
  antiga sobre uma mais nova é bloqueada com uma mensagem clara.

## Sobre a versão do WiX Toolset (nunca cobrança recorrente, seção 146)

O SDK está fixado deliberadamente em **5.0.2** — nem numa versão 4.x mais antiga (não tem o
elemento `<Files>` usado para colher os arquivos publicados), nem numa 6.x/7.x mais nova. A partir
da v6.0.0, o pacote NuGet pré-compilado do WiX (`wix`/`WixToolset.Sdk`) passou a exigir aceitar um
EULA adicional (OSMFEULA — "Open Source Maintenance Fee"): uma **cobrança mensal** para quem usa
esse binário e tem faturamento anual ≥ US$10.000. Isso conflita diretamente com o princípio de
"nunca cobrança recorrente" já usado no modelo de licença vitalícia do próprio SCANOVA (seção
146) — mesmo o WiX sendo só uma ferramenta de build, nunca distribuída com o aplicativo, a taxa
recairia sobre quem compila o instalador, então importa aqui. A v5.0.2 (última da série 5.x, que
já tem o `<Files>` necessário) continua sob a licença MS-RL pura, sem esse EULA — verificado
comparando o `nuspec` de várias versões do pacote (detalhes em `THIRD_PARTY_LICENSES.md`).

## Como gerar o instalador sem precisar de uma máquina Windows própria

`.github/workflows/build-installer.yml` builda o instalador em um runner **Windows de verdade**
do GitHub Actions — útil porque nem o compilador XAML do Windows App SDK nem o WiX Toolset rodam
fora do Windows. Disparo manual (aba "Actions" do repositório → "Build Installer" → "Run
workflow"); o `SCANOVA-Setup.msi` + `SCANOVA-Setup.msi.sha256` ficam disponíveis como artefato do
run ao final (uns 5-10 minutos). Só roda quando disparado manualmente, nunca a cada push.

## Como gerar o instalador (numa máquina Windows própria)

```powershell
.\installer\Build-Installer.ps1 -ProductVersion 1.0.0
```

Isso: (1) publica `SCANOVA.App` como autocontido para `win-x64` (o cliente não precisa instalar
o .NET nem o Windows App SDK separadamente); (2) compila `SCANOVA-Setup.msi`; (3) grava
`SCANOVA-Setup.msi.sha256` ao lado — o **checksum** citado na seção 118, para o cliente conferir
a integridade do arquivo baixado.

## Limitação deste ambiente

O WiX Toolset **só roda no Windows** — confirmado ao tentar `wix build` neste ambiente Linux:
falha imediatamente com `"The WiX Toolset only supports Windows"`. Por isso `Product.wxs` e
`SCANOVA.Installer.wixproj` nunca foram compilados diretamente neste ambiente de desenvolvimento
— mas **foram, sim, compilados e iterados de verdade** contra um runner Windows real, via
`.github/workflows/build-installer.yml` (mesma solução, em espírito, já aceita para `SCANOVA.App`:
o compilador XAML nativo do Windows App SDK também só roda no Windows, documentado desde a Fase
1). Esse ciclo já encontrou e corrigiu, contra erros reais do compilador (não hipotéticos):
problemas de organização de recursos em `App.xaml`, uso inválido de `IsEnabled` em `StackPanel`
(propriedade que não existe em WinUI 3, só em `Control`), uma extensão de `IBuffer.AsStream()`
faltando um `using`, e mais de uma tentativa de autoria WiX até chegar na combinação correta de
versão do SDK (5.0.2) + elemento `<Files>` dentro de `<ComponentGroup>`. Ainda assim, a validação
final — instalar de fato o `.msi` numa máquina Windows e confirmar que o SCANOVA abre e funciona
— continua pendente, e cabe ao usuário: rode `.\installer\Build-Installer.ps1` (ou baixe o
artefato do workflow) numa máquina Windows real antes de distribuir o instalador para clientes.

O projeto do instalador não está registrado em `SCANOVA.sln` — evita o risco de um GUID de tipo
de projeto WiX incorreto corromper a leitura do arquivo de solução principal (usado por todos os
outros 10+ projetos já funcionando). Compile-o diretamente pelo caminho do `.wixproj` (via
`Build-Installer.ps1`, ou `dotnet build installer\SCANOVA.Installer\SCANOVA.Installer.wixproj`),
ou abra-o solto no Visual Studio.
