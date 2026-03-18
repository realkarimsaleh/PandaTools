public class LapsConfig
{
    [System.Text.Json.Serialization.JsonPropertyName("domain_controller")]
    public string DomainController { get; set; } = "DMC.LEEDSBECKETT.AC.UK";

    [System.Text.Json.Serialization.JsonPropertyName("import_command")]
    public string ImportCommand { get; set; } = "Import-Module LAPS";

    [System.Text.Json.Serialization.JsonPropertyName("cmdlet_name")]
    public string CmdletName { get; set; } = "Get-LapsADPassword";

    [System.Text.Json.Serialization.JsonPropertyName("validation_delay_ms")]
    public int ValidationDelayMs { get; set; } = 10000;

    //######################################
    //Load from AppConfig (which reads config.json)
    //######################################
    public static LapsConfig Load() => ConfigLoader.AppConfig.Laps;

    //######################################
    //Save back into AppConfig and persist to config.json
    //######################################
    public void Save()
    {
        var cfg = ConfigLoader.AppConfig;
        cfg.Laps = this;
        ConfigLoader.Save(cfg);
    }

    //######################################
    //Returns the first DC from the semicolon-delimited list, or ""
    //######################################
    public string GetActiveDc()
    {
        if (string.IsNullOrWhiteSpace(DomainController)) return "";
        var parts = DomainController.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : "";
    }
}