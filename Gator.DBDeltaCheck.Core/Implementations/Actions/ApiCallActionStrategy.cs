using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Gator.DBDeltaCheck.Core.Implementations.Actions;

public class ApiCallActionStrategy : IActionStrategy
{
    public string StrategyName => "ApiCallActionStrategy";

    private readonly IHttpClientFactory _httpClientFactory;

    public ApiCallActionStrategy(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task ExecuteAsync(JObject config)
    {
        var method = new HttpMethod(config["method"].Value<string>().ToUpper());
        var url = config["url"].Value<string>();
        var payload = config["payload"]?.ToString() ?? "";

        var client = _httpClientFactory.CreateClient("TestClient");
        var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode(); // Fails the test if the API call was not successful
    }

    Task<bool> IActionStrategy.ExecuteAsync(JObject config)
    {
        throw new NotImplementedException();
    }
}