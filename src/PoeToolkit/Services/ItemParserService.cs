using System.Text.RegularExpressions;
using PoeCurrencySpammer.Models;

namespace PoeCurrencySpammer.Services;

public class ItemParserService
{
    /// <summary>
    /// Check item mods against a custom pattern language.
    /// Supports: "quoted phrase", space = AND, | = OR, !pattern = NOT
    /// </summary>
    public MatchResult CheckModQuality(string itemText, string regexPattern, bool debug = false)
    {
        if (string.IsNullOrWhiteSpace(itemText) || string.IsNullOrWhiteSpace(regexPattern))
            return new MatchResult(false, string.Empty);

        var orGroups = ParsePattern(regexPattern);

        if (debug)
        {
            var groupStrs = orGroups.Select(g => $"[{string.Join(", ", g)}]");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Parsed OR groups: {string.Join(", ", groupStrs)}");
        }

        foreach (var group in orGroups)
        {
            bool groupMatches = true;

            foreach (var part in group)
            {
                if (part.StartsWith('!'))
                {
                    // Negative: should NOT match
                    string negPattern = part[1..].Trim('"');
                    if (Regex.IsMatch(itemText, negPattern, RegexOptions.IgnoreCase))
                    {
                        groupMatches = false;
                        break;
                    }
                }
                else
                {
                    // Positive: SHOULD match
                    string searchTerm = part.Trim('"');
                    if (!Regex.IsMatch(itemText, searchTerm, RegexOptions.IgnoreCase))
                    {
                        groupMatches = false;
                        break;
                    }
                }
            }

            if (groupMatches)
            {
                // Find matching lines for feedback
                var lines = itemText.Split('\n');
                var matchingLines = new List<string>();

                foreach (var line in lines)
                {
                    foreach (var part in group)
                    {
                        if (!part.StartsWith('!'))
                        {
                            string searchTerm = part.Trim('"');
                            if (Regex.IsMatch(line, searchTerm, RegexOptions.IgnoreCase))
                            {
                                matchingLines.Add(line.Trim());
                                break;
                            }
                        }
                    }
                }

                string matchText = matchingLines.Count > 0
                    ? string.Join("\n", matchingLines)
                    : $"Found match for regex: {regexPattern}";

                return new MatchResult(true, matchText);
            }
        }

        return new MatchResult(false, string.Empty);
    }

    /// <summary>
    /// Check if item meets minimum link requirement.
    /// minLinks = 0 means all sockets must be linked.
    /// </summary>
    public MatchResult CheckLinks(string itemText, int minLinks)
    {
        string? socketsData = ExtractSocketsData(itemText);
        if (socketsData is null)
            return new MatchResult(false, "No Sockets line found");

        var groups = socketsData.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var groupSizes = groups.Select(g => g.Split('-').Length).ToArray();
        int largestGroup = groupSizes.Max();
        int totalSockets = groupSizes.Sum();

        if (minLinks > 0)
        {
            if (largestGroup >= minLinks)
                return new MatchResult(true, $"Target met! {largestGroup}-link found (need {minLinks}): {socketsData}");
            return new MatchResult(false, $"Largest group: {largestGroup}-link (need {minLinks}): {socketsData}");
        }
        else
        {
            if (groups.Length == 1)
                return new MatchResult(true, $"All sockets linked: {socketsData}");
            return new MatchResult(false, $"Not fully linked. Largest: {largestGroup}/{totalSockets}: {socketsData}");
        }
    }

    /// <summary>
    /// Check if item meets chromatic color requirements.
    /// White sockets act as wildcards filling any deficit.
    /// </summary>
    public MatchResult CheckColors(string itemText, int rReq, int gReq, int bReq)
    {
        string? socketsData = ExtractSocketsData(itemText);
        if (socketsData is null)
            return new MatchResult(false, "No Sockets line found");

        int countR = socketsData.Count(c => c == 'R');
        int countG = socketsData.Count(c => c == 'G');
        int countB = socketsData.Count(c => c == 'B');
        int countW = socketsData.Count(c => c == 'W');

        int missingR = Math.Max(0, rReq - countR);
        int missingG = Math.Max(0, gReq - countG);
        int missingB = Math.Max(0, bReq - countB);
        int totalMissing = missingR + missingG + missingB;

        if (countW >= totalMissing)
            return new MatchResult(true, $"Colors matched! R:{countR} G:{countG} B:{countB} W:{countW} (Req: {rReq},{gReq},{bReq})");

        return new MatchResult(false, $"Colors: R:{countR} G:{countG} B:{countB} W:{countW}");
    }

    /// <summary>
    /// Count prefix and suffix modifiers from PoE advanced copy text.
    /// Looks for lines like: { Prefix Modifier ... } and { Suffix Modifier ... }
    /// </summary>
    public (int prefixes, int suffixes) CountPrefixSuffix(string itemText)
    {
        int prefixes = 0, suffixes = 0;
        foreach (var line in itemText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (Regex.IsMatch(trimmed, @"^\{\s*Prefix Modifier"))
                prefixes++;
            else if (Regex.IsMatch(trimmed, @"^\{\s*Suffix Modifier"))
                suffixes++;
        }
        return (prefixes, suffixes);
    }

    /// <summary>
    /// Determines if augmentation should be attempted based on the mod layout.
    /// When seeking a prefix: augment if item has only suffix(es) and no prefix.
    /// When seeking a suffix: augment if item has only prefix(es) and no suffix.
    /// When "Any": augment if either affix slot is empty (has room for another mod).
    /// </summary>
    public bool ShouldAugment(string itemText, string altAugMode)
    {
        if (altAugMode is not ("Prefix" or "Suffix" or "Any"))
            return false;

        var (prefixes, suffixes) = CountPrefixSuffix(itemText);

        return altAugMode switch
        {
            "Prefix" => prefixes == 0 && suffixes > 0,
            "Suffix" => suffixes == 0 && prefixes > 0,
            "Any" => (prefixes == 0 && suffixes > 0) || (suffixes == 0 && prefixes > 0),
            _ => false
        };
    }

    /// <summary>
    /// Check item mods against selected mods with optional min/max roll filtering.
    /// Each mod is OR'd — any single match returns success.
    /// </summary>
    public MatchResult CheckModsWithRolls(string itemText, IEnumerable<Models.SelectedMod> mods)
    {
        if (string.IsNullOrWhiteSpace(itemText))
            return new MatchResult(false, string.Empty);

        var lines = itemText.Split('\n');

        foreach (var mod in mods)
        {
            // Build a regex with a capture group for the number
            string pattern = Regex.Escape(mod.Entry.Text)
                .Replace(@"\#", @"(\d+)(?:\([\d.,-]+\))?");

            foreach (var line in lines)
            {
                var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                if (!match.Success) continue;

                // If mod has rolls and user set min/max, validate the number
                if (mod.HasRoll && match.Groups.Count > 1 && (mod.MinValue > 0 || mod.MaxValue > 0))
                {
                    if (int.TryParse(match.Groups[1].Value, out int roll))
                    {
                        if (mod.MinValue > 0 && roll < mod.MinValue) continue;
                        if (mod.MaxValue > 0 && roll > mod.MaxValue) continue;
                    }
                }

                return new MatchResult(true, line.Trim());
            }
        }

        return new MatchResult(false, string.Empty);
    }

    /// <summary>
    /// Smart check: uses roll-aware matching if selected mods exist, otherwise falls back to regex.
    /// </summary>
    public MatchResult CheckItem(string itemText, string regexPattern, IReadOnlyList<Models.SelectedMod> selectedMods)
    {
        if (selectedMods.Count > 0)
            return CheckModsWithRolls(itemText, selectedMods);
        return CheckModQuality(itemText, regexPattern);
    }

    public string? GetItemRarity(string itemText)
    {
        var match = Regex.Match(itemText, @"Rarity: (\w+)");
        return match.Success ? match.Groups[1].Value.ToLower() : null;
    }

    private static string? ExtractSocketsData(string itemText)
    {
        foreach (var line in itemText.Split('\n'))
        {
            if (line.StartsWith("Sockets:"))
                return line.Replace("Sockets:", "").Trim();
        }
        return null;
    }

    /// <summary>
    /// Parse pattern handling quotes, AND (space), OR (|), NOT (!)
    /// </summary>
    private static List<List<string>> ParsePattern(string pattern)
    {
        var orGroups = new List<List<string>>();
        var current = new List<string>();
        bool inQuotes = false;
        var currentWord = new System.Text.StringBuilder();

        bool escaped = false;
        foreach (char c in pattern)
        {
            if (escaped)
            {
                // Backslash-space: treat as literal space within the token
                if (c == ' ')
                    currentWord.Append(' ');
                else
                {
                    currentWord.Append('\\');
                    currentWord.Append(c);
                }
                escaped = false;
            }
            else if (c == '\\' && !inQuotes)
            {
                // Peek ahead: might be escaping a space
                escaped = true;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == '|' && !inQuotes)
            {
                if (currentWord.Length > 0)
                {
                    current.Add(currentWord.ToString().Trim());
                    currentWord.Clear();
                }
                if (current.Count > 0)
                {
                    orGroups.Add(current);
                    current = [];
                }
            }
            else if (c == ' ' && !inQuotes)
            {
                if (currentWord.Length > 0)
                {
                    current.Add(currentWord.ToString().Trim());
                    currentWord.Clear();
                }
            }
            else
            {
                currentWord.Append(c);
            }
        }

        // Handle trailing backslash
        if (escaped)
            currentWord.Append('\\');

        if (currentWord.Length > 0)
            current.Add(currentWord.ToString().Trim());
        if (current.Count > 0)
            orGroups.Add(current);

        return orGroups;
    }
}
