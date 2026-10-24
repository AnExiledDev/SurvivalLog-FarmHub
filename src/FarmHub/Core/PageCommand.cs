using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FarmHub.Core;

/// <summary>A message from the page. Everything else the page could send is ignored.</summary>
public abstract record PageCommand
{
    public const string Prefix = "slmod|farmhub|";

    private PageCommand()
    {
    }

    public sealed record Toggle : PageCommand;

    public sealed record Close : PageCommand;

    public sealed record Ready : PageCommand;

    public sealed record NoScript : PageCommand;

    /// <summary>A diagnostic line from the page script, for the log.</summary>
    public sealed record Note(string Text) : PageCommand;

    /// <summary>A text box in the page gained or lost focus, so the game's keys should pause or resume.</summary>
    public sealed record Focus(bool IsTyping) : PageCommand;

    /// <summary>Null pot ids plant every empty pot.</summary>
    public sealed record Plant(IReadOnlyList<long>? PotIds) : PageCommand;

    /// <summary>Null pot ids fertilize every growing pot that has none.</summary>
    public sealed record Fertilize(IReadOnlyList<long>? PotIds) : PageCommand;

    public sealed record Tend(long PotId) : PageCommand;

    public sealed record TendAll : PageCommand;

    /// <summary>An empty preset id puts the pots back on their room's or floor's preset.</summary>
    public sealed record Assign(string PresetId, IReadOnlyList<long> PotIds) : PageCommand;

    public sealed record SavePreset(string Json) : PageCommand;

    public sealed record DeletePreset(string PresetId) : PageCommand;

    public sealed record RotWarn(int Hours) : PageCommand;

    public sealed record Robots(bool IsKeepingStocked) : PageCommand;

    /// <summary>The plant panel's "from presets" box: Tend Garden also replants, plants and fertilizes from the presets.</summary>
    public sealed record FromPresets(bool IsOn) : PageCommand;

    /// <summary>One of a robot's three sow settings: a seed and a fertilizer, each 0 for none.</summary>
    public sealed record RobotTier(long RobotId, int Tier, int SeedId, int FertId) : PageCommand;

    public static bool IsOurs(string? message) => message != null && message.StartsWith(Prefix, System.StringComparison.Ordinal);

    public static PageCommand? Parse(string? message)
    {
        if (!IsOurs(message)) return null;

        var body = message!.Substring(Prefix.Length);

        // The preset's JSON may hold '|' in its name, so it is everything after the first one.
        if (body.StartsWith("savepreset|", System.StringComparison.Ordinal)) return new SavePreset(body.Substring("savepreset|".Length));

        if (body.StartsWith("diag|", System.StringComparison.Ordinal)) return new Note(body.Substring("diag|".Length));

        var parts = body.Split('|');

        return (parts[0], parts.Length) switch
        {
            ("toggle", 1) => new Toggle(),
            ("close", 1) => new Close(),
            ("ready", 1) => new Ready(),
            ("noscript", 1) => new NoScript(),
            ("focus", 2) when parts[1] is "0" or "1" => new Focus(parts[1] == "1"),
            ("plantall", 1) => new Plant(null),
            ("plant", 2) when TryIds(parts[1], out var ids) => new Plant(ids),
            ("fertall", 1) => new Fertilize(null),
            ("fert", 2) when TryIds(parts[1], out var ids) => new Fertilize(ids),
            ("tend", 2) when TryId(parts[1], out var id) => new Tend(id),
            ("tendall", 1) => new TendAll(),
            ("assign", 3) when TryIds(parts[2], out var ids) => new Assign(parts[1], ids),
            ("deletepreset", 2) when parts[1].Length > 0 => new DeletePreset(parts[1]),
            ("rotwarn", 2) when int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) && hours is >= 1 and <= 96 => new RotWarn(hours),
            ("robots", 2) when parts[1] is "0" or "1" => new Robots(parts[1] == "1"),
            ("frompresets", 2) when parts[1] is "0" or "1" => new FromPresets(parts[1] == "1"),
            ("robottier", 5) when TryId(parts[1], out var robotId) && TryCount(parts[2], out var tier) && tier <= 2 && TryCount(parts[3], out var seedId) && TryCount(parts[4], out var fertId)
                => new RobotTier(robotId, tier, seedId, fertId),
            _ => null,
        };
    }

    private static bool TryId(string text, out long id) => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out id);

    private static bool TryCount(string text, out int count) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out count);

    private static bool TryIds(string text, out IReadOnlyList<long> ids)
    {
        var parsed = new List<long>();

        foreach (var part in text.Split(',').Where(part => part.Length > 0))
        {
            if (!TryId(part, out var id))
            {
                ids = parsed;
                return false;
            }

            parsed.Add(id);
        }

        ids = parsed;

        return parsed.Count > 0;
    }
}
