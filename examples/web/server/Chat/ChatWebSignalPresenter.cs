using Microsoft.AspNetCore.SignalR;
using HaaS.Host.Web.Infrastructure;

namespace HaaS.Host.Web.Chat;

public class ChatWebSignalPresenter : WebSignalPresenter<ChatHub>
{
    public ChatWebSignalPresenter(IHubContext<ChatHub> hubContext) 
        : base(hubContext, "chat") { }
}
