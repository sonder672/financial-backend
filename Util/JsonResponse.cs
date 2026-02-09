using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace FinancialApp.Backend.Util;

public static class JsonResponse
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<HttpResponseData> Create(
        HttpRequestData req,
        HttpStatusCode statusCode,
        object? body = null)
    {
        if (body == null && statusCode == HttpStatusCode.OK)
            statusCode = HttpStatusCode.NoContent;

        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");

        if (body != null)
        {
            object bodyToSerialize = body is string str
                ? new { response = str }
                : body;

            var json = JsonSerializer.Serialize(bodyToSerialize, JsonOptions);
            await response.WriteStringAsync(json);
        }

        return response;
    }
}
