using System.Threading.Tasks;
using System.Web.Mvc;

using TelegramNotiBot.Core;

namespace TelegramNotiBot.Controllers
{
    public class SendController : Controller
    {
        [HttpPost]
        [Route("send")]
        public async Task<string> Msg(string message, string chatName)
        {
            var result = await BotCore.SendMessage(message, chatName);
            return result;
        }
    }
}