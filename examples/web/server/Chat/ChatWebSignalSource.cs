namespace HaaS.Host.Web.Chat;

public class ChatWebSignalSource : WebSignalSource
{
    public ChatWebSignalSource(WebSignalBus bus) : base("chat", bus) { }
}
