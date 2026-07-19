using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Host.Web;

public class SessionContextRunSessionUseCaseDecorator : IRunSessionUseCase
{
    private readonly IRunSessionUseCase _inner;
    private readonly ScopedSessionContext _context;

    public SessionContextRunSessionUseCaseDecorator(IRunSessionUseCase inner, ScopedSessionContext context)
    {
        _inner = inner;
        _context = context;
    }

    public async Task<SessionResult> ExecuteAsync(Signal signal, ISignalPresenter presenter)
    {
        _context.SessionId = signal.SessionId;
        return await _inner.ExecuteAsync(signal, presenter);
    }
}
