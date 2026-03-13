using System.IO;
using System.Reflection;
using System.Text.Json;
using PoeCurrencySpammer.Models;

namespace PoeCurrencySpammer.Services;

public class StatsLoaderService
{
    private List<StatEntry>? _allStats;
    private Dictionary<string, List<StatEntry>>? _byCategory;

    public IReadOnlyList<StatEntry> AllStats => _allStats ??= LoadStats();
    public IReadOnlyDictionary<string, List<StatEntry>> ByCategory => _byCategory ??= BuildCategoryMap();

    /// <summary>
    /// Search mods by text. Returns up to maxResults matching entries.
    /// </summary>
    public List<StatEntry> Search(string query, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<StatEntry>();

        foreach (var stat in AllStats)
        {
            var lower = stat.Text.ToLowerInvariant();
            if (terms.All(t => lower.Contains(t)))
            {
                results.Add(stat);
                if (results.Count >= maxResults) break;
            }
        }

        return results;
    }

    /// <summary>
    /// Convert selected mod texts to a regex pattern for the item parser.
    /// Each mod becomes an OR group. The '#' placeholder becomes '\d+' to match numbers.
    /// </summary>
    public static string ModsToRegex(IEnumerable<StatEntry> selectedMods)
    {
        var parts = new List<string>();
        foreach (var mod in selectedMods)
        {
            // Escape regex special chars, then replace # placeholder with a pattern
            // that matches PoE clipboard format: e.g. "20(20-19)" where the roll range is optional
            string pattern = System.Text.RegularExpressions.Regex.Escape(mod.Text)
                .Replace(@"\#", @"\d+(\([\d.,-]+\))?");
            parts.Add(pattern);
        }
        return string.Join("|", parts);
    }

    private List<StatEntry> LoadStats()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("poe_stats.json"))
            ?? throw new FileNotFoundException("poe_stats.json not found in embedded resources");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var doc = JsonDocument.Parse(stream);

        var stats = new List<StatEntry>();
        var root = doc.RootElement;

        foreach (var category in root.GetProperty("result").EnumerateArray())
        {
            string catId = category.GetProperty("id").GetString()!;
            string catLabel = category.GetProperty("label").GetString()!;

            foreach (var entry in category.GetProperty("entries").EnumerateArray())
            {
                string id = entry.GetProperty("id").GetString()!;
                string text = entry.GetProperty("text").GetString()!;
                string type = entry.GetProperty("type").GetString()!;
                stats.Add(new StatEntry(id, text, type, catLabel));
            }
        }

        return stats;
    }

    private Dictionary<string, List<StatEntry>> BuildCategoryMap()
    {
        return AllStats
            .GroupBy(s => s.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
