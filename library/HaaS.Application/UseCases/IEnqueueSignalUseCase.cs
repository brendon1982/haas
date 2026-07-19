using HaaS.Domain.ValueObjects;

namespace HaaS.Application.UseCases;

public interface IEnqueueSignalUseCase
{
    Task<string> ExecuteAsync(Signal signal);
}
