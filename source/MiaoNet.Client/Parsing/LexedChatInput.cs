namespace Celeste.Mod.MiaoNet;

/// <summary>
/// The result of lexing chat input: the flat token stream plus the resolved command structure.
/// Shared by the completion provider and the input highlighter.
/// </summary>
public sealed class LexedChatInput
{
    public string Text { get; }

    /// <summary>Tokens covering every non-separator character of <see cref="Text"/> in order.</summary>
    public IReadOnlyList<ChatInputToken> Tokens { get; }

    public MiaoNetCommand? Command { get; }

    public IReadOnlyList<CommandArgumentInfo> Arguments { get; }

    /// <summary>
    /// The deepest syntax that governs the arguments, which a subcommand descent may have
    /// replaced. It applies from <see cref="AppliedSyntaxStart"/> on.
    /// </summary>
    public CommandSyntax? AppliedSyntax { get; }

    /// <summary>The index in <see cref="Arguments"/> where <see cref="AppliedSyntax"/> starts.</summary>
    public int AppliedSyntaxStart { get; }

    public LexedChatInput(
        string text,
        IReadOnlyList<ChatInputToken> tokens,
        MiaoNetCommand? command,
        IReadOnlyList<CommandArgumentInfo> arguments,
        CommandSyntax? appliedSyntax,
        int appliedSyntaxStart
    )
    {
        Text = text;
        Tokens = tokens;
        Command = command;
        Arguments = arguments;
        AppliedSyntax = appliedSyntax;
        AppliedSyntaxStart = appliedSyntaxStart;
    }

    /// <summary>
    /// The segment governing the argument at <paramref name="index"/>, or <see langword="null"/>
    /// when it lies beyond the applied syntax (an overflow argument).
    /// </summary>
    public CommandSegment? SegmentAt(int index)
    {
        if (AppliedSyntax is null)
            return null;

        int local = index - AppliedSyntaxStart;
        return local >= 0 && local < AppliedSyntax.Segments.Count
            ? AppliedSyntax.Segments[local]
            : null;
    }
}
