# Licenciamento

> **Status:** Fase 10 concluída — licença vitalícia ativada por chave assinada digitalmente,
> validação e armazenamento seguro 100% locais (sem servidor de ativação).

## Modelo comercial (seção 54-56, 146)

Licença **vitalícia** por versão adquirida: sem assinatura, sem mensalidade, sem limite
artificial de uso, sem necessidade de conexão com a internet para o SCANOVA funcionar (nem para
ativar, nem para continuar usando depois de ativado). O cliente recebe uma **chave de licença**
(uma string de texto) após a compra e a cola no aplicativo, em Configurações → Licença.

`LicenseInfo.ActivationLimit` (quantas máquinas a licença cobre) é **informativo** — não é
imposto tecnicamente pelo aplicativo. Impor isso de verdade exigiria um servidor central que
soubesse quantas vezes cada licença já foi ativada, o que contradiria o funcionamento 100%
offline. Essa é uma decisão consciente: o modelo prioriza nunca depender de infraestrutura do
fabricante (custo zero de manutenção de servidor, e o aplicativo nunca para de funcionar por uma
falha de rede ou o fabricante encerrar as atividades).

## Como a chave de licença funciona

A chave de licença é um **envelope binário assinado digitalmente** (ECDSA P-256/SHA-256),
codificado em Base64Url. Formato (versão 1):

```
[1 byte]   versão do formato
[4 bytes]  tamanho do payload JSON (inteiro little-endian)
[N bytes]  payload JSON (LicenseId, Product, Edition, CustomerName, ActivationLimit, CreatedAt)
[64 bytes] assinatura ECDSA P-256/SHA-256, formato IEEE P1363 de campo fixo (r || s)
```

O SCANOVA embute apenas a **chave pública** correspondente (`LicenseSigningPublicKey`,
`SCANOVA.Licensing/Signing`) — o suficiente para **verificar** que uma chave foi genuinamente
assinada pelo fabricante, mas insuficiente para **forjar** uma nova chave. A chave **privada**
nunca fica neste repositório nem em nenhum artefato distribuído com o aplicativo: ela existe
apenas fora do controle de versão, guardada pelo fabricante (ex.: um gerenciador de senhas/cofre),
e é usada exclusivamente pela ferramenta `tools/SCANOVA.LicenseTool` para emitir novas licenças.
Esse é o mecanismo que permite validar autenticidade sem embutir nenhum segredo criptográfico no
código-fonte (seção citada no rascunho original desta fase).

`SCANOVA.Licensing` só sabe **decodificar/verificar** — nunca assina nada; `tools/SCANOVA.LicenseTool`
é uma segunda implementação independente do mesmo formato, especializada em **emitir**. As duas
nunca compartilham código (nem mesmo via `InternalsVisibleTo`) — reflete a separação real entre
"quem confere uma licença" (o aplicativo, em qualquer máquina de cliente) e "quem consegue
criar uma licença nova" (só o fabricante, offline, com a chave privada).

## Gerando e rotacionando as chaves (uso do fabricante)

```bash
# Gera um novo par de chaves. Grava a chave PRIVADA em <pasta>/scanova-license-private-key.pem
# — GUARDE em local seguro, NUNCA versione este arquivo — e imprime a chave PÚBLICA (Base64) que
# deve substituir PublicKeyBase64 em src/SCANOVA.Licensing/Signing/LicenseSigningPublicKey.cs.
dotnet run --project tools/SCANOVA.LicenseTool -- gerar-chaves <pasta-destino>

# Emite uma nova chave de licença assinada com a chave privada gerada acima.
dotnet run --project tools/SCANOVA.LicenseTool -- emitir <chave-privada.pem> <licenseId> <edition> \
  [--cliente "Nome do cliente"] [--limite N]
```

`<licenseId>` deve ser único por licença emitida — é o identificador usado para revogar uma
licença específica (ver abaixo), então vale manter um registro externo (fora deste repositório)
de qual `licenseId` foi emitido para qual cliente/venda.

Rotacionar a chave (gerar um novo par e trocar a pública embutida) invalida **todas** as licenças
já emitidas com a chave antiga — só deve ser feito em caso de suspeita de comprometimento da
chave privada, com um plano de reemissão para os clientes existentes.

## Revogação (fraude, estorno)

`src/SCANOVA.Licensing/Revocation/RevokedLicenses.json` é um array JSON simples de `LicenseId`,
embutido como recurso no aplicativo e consultado inteiramente offline — nunca há uma chamada de
rede para verificar revogação (mesmo princípio de "nunca liga para casa" já usado no OCR, seção
40). O custo desse desenho 100% offline: uma revogação só tem efeito prático quando o cliente
atualiza para uma versão do aplicativo que já embute o `LicenseId` na lista — compromisso aceito
para manter o SCANOVA sem nenhuma dependência de infraestrutura de rede.

Uma licença revogada tem `LicenseStatus.Revoked` — diferente de `Invalid` (assinatura não
confere/corrompida): a assinatura de uma licença revogada é genuína, ela só foi
proativamente colocada na lista negra pelo fabricante.

## Armazenamento seguro (`SecureLicenseStore`)

A chave de licença ativada é gravada em `AppPaths.LicenseFilePath`, protegida por **DPAPI**
(Windows Data Protection API, escopo `CurrentUser`) — os bytes gravados só podem ser
descriptografados pela mesma conta do Windows, na mesma máquina. Isso impede, sem precisar
embutir nenhum identificador de hardware na própria licença, que o arquivo simplesmente copiado
para outra máquina/conta continue "ativado" ali: a descriptografia falha e o SCANOVA trata como
se não houvesse licença armazenada.

Fora do Windows, `ProtectedData` lança `PlatformNotSupportedException` — `SecureLicenseStore`
degrada automaticamente para gravação sem criptografia (mesmo princípio de degradação graciosa
já usado por `WiaScannerService` na Fase 4). Isso só é exercitado pelos testes automatizados
deste ambiente cross-platform: o aplicativo em si (SCANOVA.App, WinUI 3) só roda no Windows,
onde o caminho DPAPI é sempre o usado de fato.

## `ILicenseService` (`LicenseService`, `SCANOVA.Licensing`)

| Método | Comportamento |
|---|---|
| `ActivateAsync(chave)` | Decodifica, verifica assinatura, confere produto e revogação; só persiste se `LicenseStatus.Licensed`. Devolve `ProcessingResult` com mensagem amigável em caso de falha. |
| `GetLicenseStatus()` / `IsLicensed()` | Recarrega e revalida a chave armazenada a cada chamada (nunca confia em um resultado em cache) — permite que uma atualização da lista de revogação tenha efeito assim que o aplicativo reiniciar, mesmo sem reativar. |
| `GetLicenseInfo()` | Só popula campos quando a assinatura foi verificada com sucesso — nunca expõe dados de um payload cuja autenticidade não pôde ser confirmada, mesmo que o JSON seja interpretável. |
| `DeactivateAsync()` | Apaga o arquivo armazenado (melhor esforço, nunca lança). Uso pretendido: liberar uma ativação nesta máquina antes de ativar em outra, dentro do limite informativo da licença. |

## SCANOVA.App — tela "Configurações" (Fase 10)

Item "Configurações" do menu lateral (antes um placeholder genérico) agora abre uma tela real com
duas seções: **Licença** (status atual, campo para colar a chave, "Ativar licença"/"Desativar
licença nesta máquina") e **Sobre** (nome e versão do aplicativo). As demais seções de
configurações (aparência, acessibilidade etc.) ficam para a Fase 11 (Polimento) — a tela já foi
estruturada para crescer com novas seções sem precisar ser reescrita.

## Testes (`SCANOVA.Licensing.Tests`)

Nunca usa a chave pública de produção real — cada teste gera seu próprio par de chaves ECDSA
descartável, então nenhuma licença assinada em teste tem qualquer relação com uma licença real.

- **`LicenseKeyValidatorTests`**: assinatura válida com informações corretas, assinatura de uma
  chave diferente da configurada, payload adulterado após a assinatura, produto incorreto (com
  informações ainda expostas, porque a assinatura é genuína), licença revogada, chave malformada,
  versão de envelope não suportada.
- **`SignedLicenseEnvelopeTests`**: decodificação correta do envelope, tolerância a espaço em
  branco ao redor, Base64 inválido, envelope curto demais, versão não suportada, campo de
  tamanho de payload inconsistente (incluindo negativo) — tudo sem lançar exceção.
- **`SecureLicenseStoreTests`**: gravar/carregar/apagar, ausência de arquivo, criação automática
  da pasta, e o contrato de nunca lançar mesmo em casos incomuns (caminho apontando para um
  diretório, arquivo vazio).
- **`LicenseServiceTests`**: ativação de ponta a ponta (persistência real em arquivo), rejeição
  de chave inválida/produto errado/licença revogada sem persistir nada, desativação, persistência
  através de uma segunda instância apontando para o mesmo arquivo (prova que o estado é do
  arquivo, não da instância em memória), e o caso de um arquivo armazenado corrompido.
