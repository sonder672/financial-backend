using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web;
using FinancialApp.Backend.Models;
using FinancialApp.Backend.Util;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FinancialApp.Backend.Functions.Http;

public class GetMovementsFunction
{
    private readonly Container _container;

    public GetMovementsFunction(
        CosmosClient client)
    {
        _container = client.GetContainer("financeDb", "movements");
    }

    [Function("GetMovements")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
        FunctionContext context)
    {
        var queryParams = HttpUtility.ParseQueryString(req.Url.Query);

        if (!int.TryParse(queryParams["month"], out int month) || month is < 1 or > 12)
        {
            month = DateTime.UtcNow.Month;
        }

        if (!int.TryParse(queryParams["year"], out int year) || year < 2000)
        {
            year = DateTime.UtcNow.Year;
        }

        var userId = context.Items["UserId"] as string;

        var sql = new StringBuilder(
            "SELECT * FROM c " +
            "WHERE c.userId = @userId " +
            "AND YEAR(c.date) = @year " +
            "AND MONTH(c.date) = @month");

        var queryDefinition = new QueryDefinition(sql.ToString())
            .WithParameter("@userId", userId)
            .WithParameter("@year", year)
            .WithParameter("@month", month);

        var results = new List<Movement>();
        decimal totalIncome = 0;
        decimal totalExpense = 0;

        using var iterator = _container.GetItemQueryIterator<Movement>(queryDefinition);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();

            foreach (var movement in response)
            {
                results.Add(movement);

                if (movement.Type == "income")
                    totalIncome += movement.Amount;
                else if (movement.Type == "expense")
                    totalExpense += movement.Amount;
            }
        }

        var payload = new
        {
            year,
            month,
            movements = results,
            income = totalIncome,
            expense = totalExpense,
            balance = totalIncome - totalExpense
        };

        return await JsonResponse.Create(req, HttpStatusCode.OK, payload);
    }
}
