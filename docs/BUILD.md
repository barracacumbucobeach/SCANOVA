# Build

## Requisitos

- **.NET 8 SDK** (LTS) — todas as plataformas.
- **Windows 10/11 64-bit + Visual Studio 2022 17.10+** com as workloads:
  - ".NET Desktop Development"
  - "Windows App SDK C#/WinRT" (component individual: "Windows App SDK C# Templates")
  - Windows 10 SDK (10.0.19041.0 ou superior)
  — necessários apenas para compilar `SCANOVA.App` (a UI WinUI 3).

## Build cross-platform (sem a UI) — Linux, macOS, CI, ou Windows sem VS

Todas as camadas exceto `SCANOVA.App` são bibliotecas .NET 8 puras. Use o filtro de solução
`SCANOVA.CrossPlatform.slnf`:

```bash
dotnet restore SCANOVA.CrossPlatform.slnf
dotnet build SCANOVA.CrossPlatform.slnf -c Release
dotnet test SCANOVA.CrossPlatform.slnf
```

Isso restaura, compila e testa `Core`, `Infrastructure`, `Imaging`, `Tiff`, `Pdf`, `Ocr`,
`Scanner`, `Licensing` e todos os projetos de teste correspondentes — sem tocar em
`SCANOVA.App`.

### Por que `SCANOVA.App` não entra nesse comando

`SCANOVA.App` tem `TargetFramework=net8.0-windows10.0.19041.0` e usa o compilador de XAML do
Windows App SDK (`XamlCompiler.exe`), que é um executável nativo do Windows. Tentar compilá-lo
fora do Windows falha com um destes erros, dependendo da configuração:

- `NETSDK1100: To build a project targeting Windows on this operating system, set the
  EnableWindowsTargeting property to true.` (sem a flag)
- `Exec format error` ao tentar rodar o `XamlCompiler.exe` (mesmo com a flag acima) — este é o
  ponto em que a compilação realmente depende de Windows; não há workaround.

Isso é esperado e não indica um problema no projeto — apenas que a etapa final de compilação de
XAML precisa do Windows.

## Build completo (Windows, com a UI)

```powershell
dotnet restore SCANOVA.sln
dotnet build SCANOVA.sln -c Release
dotnet test SCANOVA.sln
```

Ou abra `SCANOVA.sln` no Visual Studio 2022 e compile normalmente (F6/Ctrl+Shift+B).

## Debug vs. Release

- **Debug**: símbolos completos, sem otimizações — uso durante o desenvolvimento.
- **Release**: build otimizado, usado para gerar os artefatos de publicação/instalador
  (seção 118 da especificação). A geração do instalador (MSIX/instalador tradicional +
  checksum) será formalizada na Fase 11.

## Executar apenas os testes de uma camada

```bash
dotnet test tests/SCANOVA.Tiff.Tests
```
