using Microsoft.Extensions.AI;

namespace HaaS.Adapters.Agent;

public interface IChatClientFactory
{
    bool CanCreate(string provider);
    Task<IChatClient> CreateAsync(string provider, string modelId);
}
