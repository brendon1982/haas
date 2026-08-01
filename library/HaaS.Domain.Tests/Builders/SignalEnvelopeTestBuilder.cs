using HaaS.Domain.ValueObjects;

namespace HaaS.Domain.Tests.Builders;

public sealed class SignalEnvelopeTestBuilder
{
    private Signal _signal = SignalTestBuilder.Create().Build();
    private SignalContext _context = SignalContextTestBuilder.Create().Build();

    private SignalEnvelopeTestBuilder() { }

    public static SignalEnvelopeTestBuilder Create() => new();

    public SignalEnvelopeTestBuilder WithSignal(Signal signal)
    {
        _signal = signal;
        return this;
    }

    public SignalEnvelopeTestBuilder WithContext(SignalContext context)
    {
        _context = context;
        return this;
    }

    public SignalEnvelope Build() => new(_signal, _context);
}
