# Conversão em lote

> **Status:** Fase 8 concluída — fila, progresso, pausa/retomada, cancelamento e relatório final.

## SCANOVA.Batch.BatchProcessingService (`IBatchProcessingService`)

Contrato já declarado desde a Fase 1 (`BatchJob`, `BatchItem`, `BatchProgress`,
`ProcessingStatus`): processa um `BatchJob` — N arquivos de origem, uma `ExportSettings` comum —
um item por vez, liberando cada imagem assim que o item termina (nunca carrega o lote inteiro em
memória de uma vez). Nunca apaga/modifica os arquivos de origem; cada item vira exatamente um
arquivo de saída, com o **mesmo nome do arquivo de origem** (só a extensão muda) — o padrão de
nome único de `ExportSettings.FileNamePattern`/`ResolveFileName` é para o fluxo de "Salvar como"
de um único documento; num lote de N arquivos, nomear todos a partir do mesmo timestamp
colidiria.

Só depende de `SCANOVA.Core` (nenhuma referência a `Imaging`/`Tiff`/`Pdf`): `IImageLoader`,
`IImageExporter`, `IDocumentEnhancementService`, `ITiffEncoder`, `ITiffDocumentPipeline` e
`IPdfService` são todas interfaces — a implementação concreta é resolvida pela injeção de
dependência no composition root, o mesmo padrão já usado por `SCANOVA.Pdf` (que depende só de
`ITiffEncoder`, sem referenciar `SCANOVA.Tiff`).

### Pipeline por item

```text
Carregar (IImageLoader) → Aplicar ajustes do lote (IDocumentEnhancementService.Apply) → Codificar no formato pedido
```

O formato de saída (`ExportSettings.Format`) decide qual serviço codifica o resultado:

| `OutputFormat` | Serviço usado | Observação |
|---|---|---|
| `TiffDocumental` / `TiffMultiPage` | `ITiffDocumentPipeline.SaveDocumentalTiffAsync` | "Multipágina" não se aplica item a item num lote (cada item vira um arquivo) — tratado como o preset Documental de página única. |
| `Tiff` (genérico) | `ITiffEncoder.EncodeAsync` | Preserva o modo de cor da imagem já ajustada; compressão sem perdas (CCITT G4 se bilevel, LZW caso contrário). |
| `Png` / `Jpg` | `IImageExporter.SaveAsync` | |
| `Pdf` | `IPdfService.WritePdfAsync` | Um PDF de página única por item; o modo (Documental/Cinza/Cor) é inferido do formato de pixel da imagem já ajustada, a menos que `ExportSettings.Pdf` informe um explicitamente. |
| `PdfSearchable` | `IOcrService.RecognizeAsync` + `IPdfService.WritePdfAsync` | Fase 9: reconhece o texto da imagem já ajustada (idioma/orientação de `ExportSettings.Ocr`, ou `OcrSettings.Default` quando nulo) e gera o PDF com a camada de texto invisível — como qualquer outro passo do pipeline, uma falha aqui (ex.: falha no OCR) marca só aquele item como `Failed`, sem derrubar o lote. |

Uma falha em um item (arquivo corrompido, arquivo ausente, formato incompatível com o modo
escolhido) nunca derruba o lote inteiro: vira `BatchItem.Status = Failed` com uma mensagem
amigável em `ErrorMessage`, e o processamento continua para o próximo item — o relatório final
reflete cada item individualmente (`BatchJob.SuccessCount`/`FailureCount`).

### Proteção contra sobrescrita

Se `ExportSettings.PromptBeforeOverwrite` é verdadeiro e o arquivo de destino já existe, o item
falha com uma mensagem clara em vez de perguntar ao usuário no meio de uma fila em segundo
plano (perguntar interativamente ali não faz sentido) ou sobrescrever silenciosamente — a
decisão de perguntar/confirmar antes é responsabilidade da UI, **antes** de iniciar o lote.

### Pausa e cancelamento (`PauseGate`)

`Pause(jobId)`/`Resume(jobId)` operam sobre um `SemaphoreSlim(1, 1)` interno por lote: pausar
retira o único "cadastro" disponível (qualquer espera subsequente bloqueia, de forma
assíncrona — sem ocupar uma thread); retomar o devolve. O laço em `RunAsync` verifica esse
portão **entre** itens (nunca no meio do processamento de um item já em andamento).

Um `CancellationToken` cancelado interrompe o laço na próxima verificação — mesmo enquanto
pausado (`WaitWhilePausedAsync` respeita o token). Itens que ainda não tinham terminado quando o
cancelamento chegou (incluindo os que nunca chegaram a começar) ficam marcados como
`Cancelled` — nunca "pendente" para sempre no relatório final.

## SCANOVA.App — tela "Converter em lote" (Fase 8)

Nova página (Dashboard → cartão "Converter", ou item de navegação "Converter"):
`BatchConvertViewModel` monta um `BatchJob` a partir de arquivos escolhidos (seletor com
múltipla seleção, reaproveitado da Fase 7), uma pasta de destino (novo `IFilePickerService
.PickFolderAsync`), um formato de saída e dois interruptores — "Melhorar automaticamente" (usa
o preset `EnhancementPreset.Normal`, o mesmo do botão "Melhorar automaticamente" no
visualizador) e "Sobrescrever arquivos existentes". Durante a execução: barra de progresso,
nome do arquivo atual, e os botões Pausar/Retomar/Cancelar. Ao final: resumo de sucesso/falha e
a lista dos itens que falharam, cada um com sua mensagem de erro.

## Testes (`SCANOVA.Batch.Tests`)

Só o projeto de testes referencia `SCANOVA.Imaging`/`SCANOVA.Tiff`/`SCANOVA.Pdf`/`SCANOVA.Ocr` —
serviços reais (não mocks, incluindo o `TesseractOcrService` de verdade), testando o lote de
ponta a ponta contra os pipelines já existentes: sucesso em múltiplos formatos (TIFF/JPG/PDF, com
verificação de contagem de páginas do PDF via `IPdfRasterizer`), preservação do nome de arquivo,
relatório de progresso, resiliência a um item com falha, `PdfSearchable` de ponta a ponta (OCR de
verdade sobre uma imagem sintética com texto, seguido de re-extração do texto do PDF gerado via
`TryExtractTextAsync`) incluindo o caso de falha isolada por item (arquivo de origem ausente),
proteção/permissão de sobrescrita, criação automática da pasta de destino, aplicação dos ajustes
do lote, e pausa/cancelamento — incluindo cancelar **enquanto pausado** (o caso que mais
facilmente travaria um laço mal-implementado).
