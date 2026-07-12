using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public interface IRunSessionUseCase
{
    Task<string> ExecuteAsync(Signal signal, ISignalPresenter presenter);
}
