using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web.Chat;

public class ChatWebSignalPresenter : WebSignalPresenter<ChatHub>
{
    public ChatWebSignalPresenter(IHubContext<ChatHub> hubContext) 
        : base(hubContext, "chat") { }
}
