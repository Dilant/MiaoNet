using Celeste.Mod.ChatInputBox;

namespace Celeste.Mod.MiaoNet;

/// <summary>Completes a command by its name or any alias.</summary>
/// <remarks>
/// A command contributes at most one candidate: its best-matching target decides the inserted
/// spelling, while the display lists the name plus every alias that matched.
/// </remarks>
public static class CommandNameCompletion
{
    /// <param name="left">The typed name before the caret.</param>
    /// <param name="right">The rest of the name after the caret, so the caret can be mid-name.</param>
    public static IReadOnlyList<Completion> Complete(
        IReadOnlyCollection<MiaoNetCommand> commands, string left, string right, int removeRight
    )
    {
        List<(Completion Completion, int Rank)> matches = [];
        foreach (MiaoNetCommand command in commands)
        {
            if (TryMatch(command, left, right, out CommandNameMatch match))
                matches.Add((Build(command, match.Spelling, left, removeRight), match.Rank));
        }

        // OrderBy is stable, so equal ranks keep declaration order.
        return matches
            .OrderBy(m => m.Rank)
            .Select(m => m.Completion)
            .ToList();
    }

    private static bool TryMatch(
        MiaoNetCommand command, string left, string right, out CommandNameMatch best)
    {
        best = default;
        bool found = false;

        if (TryMatchTarget(command.Name, left, right, out int quality))
        {
            best = new CommandNameMatch(command.Name, Rank(quality, isAlias: false));
            found = true;
        }

        if (command.Aliases is not null)
        {
            foreach (string alias in command.Aliases)
            {
                if (!TryMatchTarget(alias, left, right, out quality))
                    continue;
                int rank = Rank(quality, isAlias: true);
                if (!found || rank < best.Rank)
                {
                    best = new CommandNameMatch(alias, rank);
                    found = true;
                }
            }
        }

        return found;
    }

    // Prefix beats inner, and the name beats an alias; the scan keeps the first best match, so
    // aliases keep declaration order.
    private static int Rank(int quality, bool isAlias) => (quality * 2) + (isAlias ? 1 : 0);

    private static bool TryMatchTarget(string target, string left, string right, out int quality)
    {
        int index = target.IndexOf(left, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            quality = 0;
            return false;
        }

        // The text after the caret must still follow the matched part, so a candidate that
        // doesn't contain it is not offered (e.g. "/ch|xyz" does not offer "channel").
        if (right.Length > 0 && target.IndexOf(right, index + left.Length, StringComparison.OrdinalIgnoreCase) < 0)
        {
            quality = 0;
            return false;
        }

        quality = index == 0 ? 0 : 1;
        return true;
    }

    private static Completion Build(
        MiaoNetCommand command, string content, string left, int removeRight)
        => new(
            content, Display(command, left), left.Length,
            shouldCloseMenu: false, removeRight: removeRight, match: left
        );

    private static string Display(MiaoNetCommand command, string left)
    {
        // Show the aliases that matched (prefix or inner), so "/w" offers "unwatch (uw, uwt)".
        List<string> matchedAliases = [];
        if (left.Length > 0 && command.Aliases is not null)
        {
            foreach (string alias in command.Aliases)
            {
                if (alias.Contains(left, StringComparison.OrdinalIgnoreCase))
                    matchedAliases.Add(alias);
            }
        }

        return matchedAliases.Count == 0
            ? command.Name
            : $"{command.Name} ({string.Join(", ", matchedAliases)})";
    }

    private readonly record struct CommandNameMatch(string Spelling, int Rank);
}
