# Instalador (Fase 11)

> **Status:** projeto do instalador (MSI, via WiX Toolset) escrito e documentado — **ainda não
> compilado nem testado de verdade** (ver "Limitação deste ambiente", abaixo). Precisa de uma
> primeira verificação numa máquina Windows antes de ser usado para distribuir o SCANOVA.

## Decisão: MSI tradicional, não MSIX (seção 91)

O SCANOVA usa um **instalador MSI tradicional** (via WiX Toolset v4), não um pacote MSIX. Motivo:
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
    ├── SCANOVA.Installer.wixproj          — projeto WiX v4 (SDK-style)
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
- **Arquivos do aplicativo colhidos automaticamente** (`HarvestDirectory` no `.wixproj`) a partir
  da pasta publicada — nunca listados à mão no `.wxs`, para nunca ficar desatualizado conforme o
  conteúdo publicado mudar entre versões.
- **Upgrade automático:** instalar uma versão mais nova sobre uma mais antiga substitui os
  arquivos automaticamente (`MajorUpgrade` em `Product.wxs`); a instalação de uma versão mais
  antiga sobre uma mais nova é bloqueada com uma mensagem clara.

## Como gerar o instalador (só no Windows)

```powershell
.\installer\Build-Installer.ps1 -ProductVersion 1.0.0
```

Isso: (1) publica `SCANOVA.App` como autocontido para `win-x64` (o cliente não precisa instalar
o .NET nem o Windows App SDK separadamente); (2) compila `SCANOVA-Setup.msi`; (3) grava
`SCANOVA-Setup.msi.sha256` ao lado — o **checksum** citado na seção 118, para o cliente conferir
a integridade do arquivo baixado.

## Limitação deste ambiente

O WiX Toolset **só roda no Windows** — confirmado ao tentar `wix build` neste ambiente Linux:
falha imediatamente com `"The WiX Toolset only supports Windows"`. Isso significa que
`Product.wxs`/`SCANOVA.Installer.wixproj` foram escritos com cuidado a partir da documentação
oficial do WiX v4, mas **nunca foram de fato compilados nem testados** neste ambiente — mesma
limitação, em espírito, já aceita para `SCANOVA.App` (compilador XAML nativo do Windows,
documentado desde a Fase 1). Trate como um primeiro rascunho sólido, não como um artefato já
validado: rode `.\installer\Build-Installer.ps1` numa máquina Windows real antes de confiar nele
para distribuir o SCANOVA, e corrija o que o WiX apontar como erro de sintaxe/autoria (esperável
em uma primeira tentativa nunca compilada).

O projeto do instalador não está registrado em `SCANOVA.sln` — evita o risco de um GUID de tipo
de projeto WiX incorreto corromper a leitura do arquivo de solução principal (usado por todos os
outros 10+ projetos já funcionando). Compile-o diretamente pelo caminho do `.wixproj` (via
`Build-Installer.ps1`, ou `dotnet build installer\SCANOVA.Installer\SCANOVA.Installer.wixproj`),
ou abra-o solto no Visual Studio.
