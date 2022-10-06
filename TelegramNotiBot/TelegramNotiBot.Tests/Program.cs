using System.Net;

namespace TelegramNotiBot.ConsoleTests
{
    class Program
    {
        static void Main(string[] args)
        {
            string url = "https://localhost:44381/send";
            string parameters = "{message: \"Please, check Shelf #10\", ChatName: \"NotiBotGroup123\"}";

            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/json; charset=utf-8";
                string htmlResult = wc.UploadString(url, "POST", parameters);
            }
        }
    }
}