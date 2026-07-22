using HaaS.Infrastructure;
using HaaS.Host.Web.Infrastructure;

namespace HaaS.Host.Web.Chat;

public static class ChatWebModule
{
    public static void AddChatWebModule(this HaasBuilder.HaasQueuedPoolBuilder pool)
    {
        pool.AddSignalSource<ChatWebSignalSource, ChatWebSignalPresenter>(config =>
        {
            config.UseProvider("openrouter")
                  .UseModel("cohere/north-mini-code:free")
                  .UseSystemPrompt("You are an assistant in a web chat. Reply concisely.");
        });
    }
}
