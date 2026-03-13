using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoeCurrencySpammer.Models;

public class AppConfig
{
    /// <summary>
    /// Transient: current selected mods with roll constraints. Not persisted.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<SelectedMod> SelectedMods { get; set; } = [];

    public string StopKey { get; set; } = "Escape";
    public string StartKey { get; set; } = "F5";
    public int MatchFoundFrequency { get; set; } = 1000;
    public int MatchFoundDuration { get; set; } = 1000;
    public double DelayAfterClick { get; set; } = 0.05;
    public string SearchRegex { get; set; } = "dic|cil|flar";
    public int ChromaticR { get; set; }
    public int ChromaticG { get; set; }
    public int ChromaticB { get; set; }
    public int MinLinks { get; set; }
    public int AutoclickerIntervalMs { get; set; } = 50;
    public string AltAugMode { get; set; } = "None"; // None, Prefix, Suffix

    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "config.json");

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }
}
