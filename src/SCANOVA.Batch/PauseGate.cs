namespace SCANOVA.Batch;

/// <summary>
/// Portão de pausa/retomada assíncrono para um único <c>BatchJob</c>, baseado em
/// <see cref="SemaphoreSlim"/>(1,1): sem pausa, o único "cadastro" fica disponível e
/// <see cref="WaitWhilePausedAsync"/> retorna quase instantaneamente; pausado, o cadastro é
/// retirado e qualquer espera bloqueia (de forma assíncrona, sem ocupar uma thread) até
/// <see cref="Resume"/> devolvê-lo.
/// </summary>
internal sealed class PauseGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _isPaused;

    public void Pause()
    {
        if (Interlocked.CompareExchange(ref _isPaused, 1, 0) == 0)
        {
            _gate.Wait();
        }
    }

    public void Resume()
    {
        if (Interlocked.CompareExchange(ref _isPaused, 0, 1) == 1)
        {
            _gate.Release();
        }
    }

    public async Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _gate.Release();
    }
}
