namespace YoloAbstractions.Config;

public record ApiConfig
{
    public string ApiBaseUrl { get; init; } = "";
    public string ApiKey { get; init; } = "";
    
    public string DateFormat { get; init; } = "yyyy-MM-dd";
}
