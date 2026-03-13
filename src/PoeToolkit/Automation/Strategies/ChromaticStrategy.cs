using System.Drawing;
using PoeCurrencySpammer.Models;
using PoeCurrencySpammer.Services;

namespace PoeCurrencySpammer.Automation.Strategies;

public class ChromaticStrategy : ICurrencyStrategy
{
    private readonly InputSimulatorService _input;
    private readonly ClipboardService _clipboard;
    private readonly ItemParserService _parser;
    private readonly AppConfig _config;

    public string ModeName => "Chromatic";
    public string[] CoordinateLabels => ["CHROMATIC ORB", "ITEM"];

    public ChromaticStrategy(InputSimulatorService input, ClipboardService clipboard,
        ItemParserService parser, AppConfig config)
    {
        _input = input;
        _clipboard = clipboard;
        _parser = parser;
        _config = config;
    }

    public MatchResult ExecuteIteration(Point[] coords, CancellationToken ct)
    {
        var currency = coords[0];
        var item = coords[1];

        _input.ReleaseModifiers();
        _input.FastClick(currency, "right");
        _input.FastClick(item, "left");

        Thread.Sleep((int)((_config.DelayAfterClick > 0 ? _config.DelayAfterClick : 0.05) * 1000));

        if (ct.IsCancellationRequested)
            return new MatchResult(false, string.Empty);

        // Must reliably read before allowing next currency application
        var itemText = _clipboard.CopyItemTextReliable(ct);
        if (!string.IsNullOrEmpty(itemText))
            return _parser.CheckColors(itemText, _config.ChromaticR, _config.ChromaticG, _config.ChromaticB);

        return new MatchResult(false, string.Empty);
    }
}
