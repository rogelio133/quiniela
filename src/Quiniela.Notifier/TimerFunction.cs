using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Quiniela.Notifier;

/// <summary>
/// Reloj externo para el App Service F1 (sin Always On la app se duerme y ningún
/// BackgroundService sobrevive). Cada 10 minutos hace POST a /api/notify/check, lo que
/// despierta la app y dispara NotificationCheckService (N2 y N5). Toda la lógica de
/// notificación vive en la Blazor app; esta function solo pingea.
/// </summary>
public class TimerFunction(IHttpClientFactory httpFactory, IConfiguration config, ILogger<TimerFunction> logger)
{
    [Function("NotifyCheck")]
    public async Task Run([TimerTrigger("0 */10 * * * *")] TimerInfo timer)
    {
        var url = config["AppService:NotifyUrl"];
        var secret = config["AppService:NotifySecret"];

        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(secret))
        {
            logger.LogError("AppService:NotifyUrl y/o AppService:NotifySecret no están configurados.");
            return;
        }

        var client = httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Notify-Secret", secret);

        var response = await client.SendAsync(request);
        logger.LogInformation("Ping a {Url} respondió {StatusCode}", url, (int)response.StatusCode);
    }
}
