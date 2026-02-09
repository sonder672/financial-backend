using Newtonsoft.Json;

namespace FinancialApp.Backend.Models;

public class Movement
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    private string _type = "expense";
    public string Type
    {
        get => _type;
        set
        {
            if (string.Equals(value, "income", StringComparison.OrdinalIgnoreCase))
                _type = "income";
            else if (string.Equals(value, "expense", StringComparison.OrdinalIgnoreCase))
                _type = "expense";
            else
                throw new ArgumentException("Type must be 'income' or 'expense'");
        }
    }

    private string _category = "otros";
    public string Category
    {
        get => _category;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _category = "otros";
                return;
            }

            if (ValidCategories.Contains(value))
                _category = value.ToLowerInvariant();
            else
                _category = "otros";
        }
    }

    private decimal _amount;
    public decimal Amount
    {
        get => _amount;
        set
        {
            _amount = Math.Abs(value);
        }
    }

    public string Description { get; set; } = string.Empty;
    
    [JsonProperty("date")]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    private static readonly HashSet<string> ValidCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "comida",
            "transporte",
            "entretenimiento",
            "servicios",
            "otros"
        };
}