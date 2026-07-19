using HaaS.Domain.Ports;

namespace HaaS.Infrastructure;

internal sealed class ErrorPresentingSignalHandle : ISignalHandle
{
    private readonly ISignalHandle _inner;
    private readonly ISignalPresenter _presenter;

    public ErrorPresentingSignalHandle(ISignalHandle inner, ISignalPresenter presenter)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
    }

    public string SessionId => _inner.SessionId;

    public async Task WaitForResultAsync(CancellationToken ct = default)
    {
        try
        {
            await _inner.WaitForResultAsync(ct);
        }
        catch (Exception ex)
        {
            await _presenter.PresentErrorAsync(SessionId, ex);
            throw;
        }
    }
}
