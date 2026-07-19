using System;

namespace HaaS.Infrastructure;

internal class SignalSourceOptions
{
    public Type SourceType { get; }
    public bool IsQueued { get; set; }

    public SignalSourceOptions(Type sourceType)
    {
        SourceType = sourceType;
    }
}
