using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web.Chat;

public class ChatWebSignalPresenter : WebSignalPresenter
{
    public ChatWebSignalPresenter(IHubContext<HaaSWebHub> hubContext) 
        : base(hubContext, "chat") { }
}
