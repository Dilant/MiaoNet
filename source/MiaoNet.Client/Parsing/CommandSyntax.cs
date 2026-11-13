namespace Celeste.Mod.MiaoNet;

/// <summary>
/// The syntax of a command's argument list: ordered <see cref="CommandSegment"/>s plus whether the
/// last segment captures the rest of the input as free text.
/// </summary>
/// <remarks>
/// A segment is either a plain <see cref="CommandSegmentType"/> or a subcommand selector. When a
/// selector's typed value names a declared subcommand, that subcommand's syntax governs the
/// remaining arguments, so subcommands can nest.
/// </remarks>
public sealed class CommandSyntax
{
    public IReadOnlyList<CommandSegment> Segments { get; }

    public bool CaptureRest { get; }

    public CommandSyntax(IReadOnlyList<CommandSegment> segments, bool captureRest)
    {
        Segments = segments;
        CaptureRest = captureRest;
    }

    public CommandSyntax(IReadOnlyList<CommandSegmentType> segments, bool captureRest)
        : this(ToSegments(segments), captureRest)
    {
    }

    private static CommandSegment[] ToSegments(IReadOnlyList<CommandSegmentType> segments)
    {
        CommandSegment[] result = new CommandSegment[segments.Count];
        for (int i = 0; i < segments.Count; i++)
            result[i] = new CommandSegment(segments[i]);
        return result;
    }

    /// <summary>Whether any top-level segment is a subcommand selector.</summary>
    public bool HasSubcommands
    {
        get
        {
            foreach (CommandSegment segment in Segments)
                if (segment.IsSubcommand)
                    return true;
            return false;
        }
    }
}

/// <summary>
/// One argument slot of a <see cref="CommandSyntax"/>: either a plain typed segment or a
/// subcommand selector.
/// </summary>
public sealed class CommandSegment
{
    /// <summary>The plain segment type. A subcommand selector uses <see cref="CommandSegmentType.CommandName"/>.</summary>
    public CommandSegmentType Type { get; }

    /// <summary>Subcommand name-to-syntax map for a selector, or <see langword="null"/> for a plain segment.</summary>
    public IReadOnlyDictionary<string, CommandSyntax>? Subcommands { get; }

    public CommandSegment(CommandSegmentType type)
    {
        Type = type;
    }

    public CommandSegment(IReadOnlyDictionary<string, CommandSyntax> subcommands)
    {
        Type = CommandSegmentType.CommandName;
        Subcommands = subcommands;
    }

    public bool IsSubcommand => Subcommands is not null;
}

/// <summary>Resolves a command's arguments against its <see cref="CommandSyntax"/>.</summary>
public static class CommandSyntaxResolver
{
    public static IReadOnlyList<CommandArgumentInfo> Resolve(string text, int contentStart, CommandSyntax syntax)
        => Resolve(text, contentStart, syntax, out _, out _);

    /// <summary>
    /// Resolves the arguments, also reporting the deepest syntax that governs them and the index
    /// of the first argument it governs. Without a subcommand descent these are
    /// <paramref name="syntax"/> and 0.
    /// </summary>
    public static IReadOnlyList<CommandArgumentInfo> Resolve(
        string text, int contentStart, CommandSyntax syntax,
        out CommandSyntax appliedSyntax, out int appliedStart
    )
    {
        if (!syntax.HasSubcommands)
        {
            appliedSyntax = syntax;
            appliedStart = 0;
            return ResolveFlat(text, contentStart, syntax);
        }
        return ResolveWithSubcommands(text, contentStart, syntax, out appliedSyntax, out appliedStart);
    }

    // The common case: no subcommands, so the plain argument splitter fully applies.
    private static IReadOnlyList<CommandArgumentInfo> ResolveFlat(string text, int contentStart, CommandSyntax syntax)
    {
        IReadOnlyList<CommandArgument> raw
            = CommandArgumentSplitter.Split(text, contentStart, syntax.Segments.Count, syntax.CaptureRest);

        CommandArgumentInfo[] result = new CommandArgumentInfo[raw.Count];
        for (int i = 0; i < raw.Count; i++)
        {
            bool overflow = i >= syntax.Segments.Count;
            CommandSegmentType type = overflow
                ? CommandSegmentType.Text
                : syntax.Segments[i].Type;
            bool freeText = !overflow
                && type is CommandSegmentType.Text or CommandSegmentType.Emote;
            result[i] = new CommandArgumentInfo(raw[i].Start, raw[i].End, type, freeText, false, null, overflow);
        }
        return result;
    }

    // The reserved case: walk the syntax, descending into subcommands by their typed name.
    private static IReadOnlyList<CommandArgumentInfo> ResolveWithSubcommands(
        string text, int contentStart, CommandSyntax syntax,
        out CommandSyntax appliedSyntax, out int appliedStart
    )
    {
        List<(int Start, int End)> words = SplitWords(text, contentStart);
        List<CommandArgumentInfo> args = [];
        int captureRestIndex = -1;

        appliedSyntax = syntax;
        appliedStart = 0;

        for (int wi = 0, si = 0; wi < words.Count;)
        {
            if (si >= syntax.Segments.Count)
            {
                // Beyond the declared segments: an overflow argument, not free text.
                args.Add(new CommandArgumentInfo(words[wi].Start, words[wi].End, CommandSegmentType.Text, false, false, null, true));
                wi++;
                continue;
            }

            CommandSegment segment = syntax.Segments[si];
            if (segment.IsSubcommand)
            {
                IReadOnlyDictionary<string, CommandSyntax> subcommands = segment.Subcommands!;
                string value = text[words[wi].Start..words[wi].End];
                string[] names = SubcommandNames(subcommands);
                if (TryGetSubcommand(subcommands, value, out CommandSyntax? subSyntax))
                {
                    args.Add(new CommandArgumentInfo(words[wi].Start, words[wi].End, CommandSegmentType.CommandName, false, true, names));
                    wi++;
                    syntax = subSyntax!;
                    si = 0;
                    // The arguments from here on are governed by the subcommand's syntax.
                    appliedSyntax = syntax;
                    appliedStart = args.Count;
                }
                else
                {
                    // Unknown subcommand: keep it as free text and stop descending.
                    args.Add(new CommandArgumentInfo(words[wi].Start, words[wi].End, CommandSegmentType.Text, true, true, names));
                    wi++;
                    si++;
                }
                continue;
            }

            bool captureRest = syntax.CaptureRest && si == syntax.Segments.Count - 1;
            bool freeText = segment.Type is CommandSegmentType.Text or CommandSegmentType.Emote;
            args.Add(new CommandArgumentInfo(words[wi].Start, words[wi].End, segment.Type, freeText, false, null));
            if (captureRest)
                captureRestIndex = args.Count - 1;
            wi++;
            si++;
        }

        if (captureRestIndex >= 0)
        {
            int end = text.Length;
            while (end > args[captureRestIndex].Start && char.IsWhiteSpace(text[end - 1]))
                end--;
            args[captureRestIndex] = args[captureRestIndex] with { End = end };
            args.RemoveRange(captureRestIndex + 1, args.Count - captureRestIndex - 1);
        }

        return args;
    }

    private static List<(int Start, int End)> SplitWords(string text, int contentStart)
    {
        List<(int Start, int End)> words = [];
        int i = contentStart;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            if (i >= text.Length)
                break;

            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
                i++;
            words.Add((start, i));
        }
        return words;
    }

    private static bool TryGetSubcommand(
        IReadOnlyDictionary<string, CommandSyntax> subcommands, string name, out CommandSyntax? syntax
    )
    {
        foreach (var pair in subcommands)
        {
            if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                syntax = pair.Value;
                return true;
            }
        }
        syntax = null;
        return false;
    }

    private static string[] SubcommandNames(IReadOnlyDictionary<string, CommandSyntax> subcommands)
    {
        string[] names = new string[subcommands.Count];
        int i = 0;
        foreach (string key in subcommands.Keys)
            names[i++] = key;
        return names;
    }
}
