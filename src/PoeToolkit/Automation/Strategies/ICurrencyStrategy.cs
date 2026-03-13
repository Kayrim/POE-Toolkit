using PoeCurrencySpammer.Models;

namespace PoeCurrencySpammer.Automation.Strategies;

public interface ICurrencyStrategy
{
    string ModeName { get; }

    /// <summary>
    /// The coordinate labels needed for setup (e.g., ["Alteration Orb", "Item"])
    /// </summary>
    string[] CoordinateLabels { get; }

    /// <summary>
    /// Execute one apply-and-check cycle. Returns match result.
    /// coords maps label index to captured Point.
    /// </summary>
    MatchResult ExecuteIteration(System.Drawing.Point[] coords, CancellationToken ct);
}
