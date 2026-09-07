# Performance (Fase 11)

> **Status:** revisão de código concluída. Sem regressões encontradas; um compromisso
> pré-existente foi revisado e mantido conscientemente (ver abaixo).

## Limitação desta revisão

`SCANOVA.App` (WinUI 3) só roda no Windows — este ambiente (Linux) não consegue executar o
aplicativo de verdade, então não há como fazer *profiling* real (medir tempo de tela, uso de
memória, taxa de quadros etc.) aqui. Esta revisão é, portanto, uma **auditoria de código**
(padrões conhecidos que causam lentidão perceptível numa UI desktop), não uma otimização guiada
por medição. Profiling real fica pendente de uma primeira execução em uma máquina Windows.

## O que foi verificado

- **Nenhum trabalho pesado de CPU roda direto na UI thread.** Toda operação de pixel (decodificar
  imagem, girar, cortar, binarizar, detectar documento, reconhecer texto) já passa por
  `Task.Run` antes de tocar dados, um padrão consistente desde a Fase 1 (comentários "seção
  7/61" espalhados pelo código são exatamente essa regra). Verificado nos pontos de entrada mais
  sensíveis: `SkiaImageLoader.LoadAsync`, `RasterImageBitmapConverter.ToWriteableBitmapAsync`
  (só a construção do `WriteableBitmap` em si toca a UI thread — a conversão de canais roda no
  thread pool), `TesseractOcrService.RecognizeAsync` (processo externo, nunca bloqueia).
- **Nenhum bloqueio síncrono de código assíncrono.** Busca por `.Result`/`.Wait()` no código de
  produção não encontrou nenhum uso de bloqueio de uma `Task` a partir de outra (que poderia
  causar deadlock ou travar a UI thread).
- **Nenhum `GC.Collect()` manual** (forçar coleta de lixo manualmente quase sempre piora a
  performance em vez de ajudar).

## Compromisso revisado e mantido: `PauseGate.Pause()`

`SCANOVA.Batch.PauseGate.Pause()` chama `SemaphoreSlim.Wait()` (síncrono, bloqueante) para
remover o "cadastro" disponível do semáforo. Na janela extremamente estreita em que
`WaitWhilePausedAsync` já adquiriu o semáforo mas ainda não o liberou (duas linhas de código,
sem nenhum trabalho real entre elas), uma chamada a `Pause()` nesse instante bloquearia a thread
chamadora (normalmente a UI thread, já que `Pause` é chamado a partir de um comando de botão) por
uma fração de milissegundo.

Decisão: **manter como está**. Tornar `Pause()` assíncrono exigiria mudar a assinatura de
`IBatchProcessingService.Pause(Guid jobId)` — uma interface pública estável desde a Fase 1,
usada pela UI (`BatchConvertViewModel`) e pelos testes — por um ganho prático inexistente (a
janela de corrida é tão estreita que, na prática, nunca chega a ser percebida por um usuário
clicando "Pausar"). Mudar uma interface pública para eliminar um bloqueio de duração
imperceptível não vale o risco/custo da mudança.

## Próximos passos (fora do escopo desta fase)

Uma primeira execução real em Windows deveria focar em: tempo de abertura de imagens grandes
(vários milhares de pixels de lado), tempo de reconhecimento de texto (Tesseract) em documentos
de página inteira, e uso de memória ao processar um lote grande (Fase 8) — o pipeline já libera
cada imagem assim que o item termina (não acumula o lote inteiro em memória), mas isso nunca foi
medido de verdade.
