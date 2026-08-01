using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;

namespace HaaS.Infrastructure;

public sealed class SignalContextScope : ISignalContextAccessor, ISignalContextScope
{
    private SignalExecutionContext? _current;

    public SignalExecutionContext Current => _current
        ?? throw new InvalidOperationException("No signal execution context is active.");

    public IDisposable Push(SignalExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_current is not null)
        {
            throw new InvalidOperationException("A signal execution context is already active.");
        }

        var previous = _current;
        _current = context;
        return new ContextLease(this, previous);
    }

    private sealed class ContextLease(SignalContextScope owner, SignalExecutionContext? previous) : IDisposable
    {
        private SignalContextScope? _owner = owner;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is not null)
            {
                owner._current = previous;
            }
        }
    }
}
