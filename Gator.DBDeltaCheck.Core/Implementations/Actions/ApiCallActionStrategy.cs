using Gator.DBDeltaCheck.Core.Abstractions;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Gator.DBDeltaCheck.Core.Implementations.Actions;

public class ApiCallActionStrategy : IActionStrategy
{
    public string StrategyName => "ApiCall";

    private readonly IHttpClientFactory _httpClientFactory;

    public ApiCallActionStrategy(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }


    /// <summary>
    /// Executes an HTTP request based on the provided parameters.
    /// </summary>
    /// <returns>True if the API call returns a success status code; otherwise, it throws an exception.</returns>
    public async Task<bool> ExecuteAsync(JObject parameters)
    {

        var method = new HttpMethod(parameters["Method"]?.Value<string>()?.ToUpper()
            ?? throw new ArgumentException("'Method' is missing from ApiCall parameters."));

        var endpoint = parameters["Endpoint"]?.Value<string>()
            ?? throw new ArgumentException("'Endpoint' is missing from ApiCall parameters.");

        var payload = parameters["Payload"]?.ToString() ?? string.Empty;

        // "TestClient" should match the name you used when registering the client in the DI fixture.
        var client = _httpClientFactory.CreateClient("TestClient");
        var request = new HttpRequestMessage(method, endpoint);

        if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
        {
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }


        var response = await client.SendAsync(request);

        // This will throw an HttpRequestException if the status code is not 2xx,
        // which will correctly fail the test with a clear error message.
        response.EnsureSuccessStatusCode();

        return true;
    }
}