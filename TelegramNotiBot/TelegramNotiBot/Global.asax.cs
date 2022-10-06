using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

using TelegramNotiBot.Core;

namespace TelegramNotiBot
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected async void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            log4net.Config.XmlConfigurator.Configure();

            await BotCore.Start();
        }
    }
}