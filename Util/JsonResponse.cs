using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FinancialApp.Backend.Util;

public static class JsonResponse
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

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

            var json = JsonConvert.SerializeObject(bodyToSerialize, JsonSettings);
            await response.WriteStringAsync(json);
        }

        return response;
    }
}
