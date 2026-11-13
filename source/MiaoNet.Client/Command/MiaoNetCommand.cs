namespace Celeste.Mod.MiaoNet;

public sealed partial class MiaoNetCommand
{
    /// <returns>Error string, or <see langword="null"/> if no error.</returns>
    public delegate string? ExecuteHandler(Context context);

    public string Name { get; }

    public IReadOnlyList<string>? Aliases { get; }

    public IReadOnlyList<CommandSegmentType> Segments { get; }

    public bool CaptureRestSegments { get; }

    /// <summary>
    /// The argument syntax used to parse this command. Normally derived from
    /// <see cref="Segments"/> and <see cref="CaptureRestSegments"/>; a command may instead pass
    /// an explicit syntax to declare (nested) subcommands.
    /// </summary>
    public CommandSyntax Syntax { get; }

    /// <summary>Where the free text this command takes ends up, if it is chat at all.</summary>
    public ChatSendScope ChatSendScope { get; }

    public ExecuteHandler OnExecute { get; }

    public MiaoNetCommand(
        string name,
        IReadOnlyList<string>? aliases,
        IReadOnlyList<CommandSegmentType> segments,
        bool captureRestSegments,
        ExecuteHandler onExecute,
        CommandSyntax? syntax = null,
        ChatSendScope chatSendScope = ChatSendScope.None
    )
    {
        Name = name;
        Aliases = aliases;
        Segments = segments;
        CaptureRestSegments = captureRestSegments;
        OnExecute = onExecute;
        Syntax = syntax ?? new CommandSyntax(segments, captureRestSegments);
        ChatSendScope = chatSendScope;

        if (captureRestSegments && segments.Count == 0)
            throw new ArgumentException(SR.SegmentsEmptyButCapture, nameof(captureRestSegments));
    }
}
