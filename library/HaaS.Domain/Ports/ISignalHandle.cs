using System.Threading;
using System.Threading.Tasks;

namespace HaaS.Domain.Ports;

public interface ISignalHandle
{
    string SessionId { get; }
    Task WaitForResultAsync(CancellationToken ct = default);
}
