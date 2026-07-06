using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public interface IChatClientFactory
{
    bool CanCreate(string provider);
    Task<IChatClient> CreateAsync(string provider, string modelId);
    void ConfigureOptions(string provider, ChatOptions options, AgentSessionConfig config);
}
