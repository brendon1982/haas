using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public interface IRunSessionUseCase
{
    Task<SessionResult> ExecuteAsync(SignalEnvelope envelope, ISignalPresenter presenter);
}
