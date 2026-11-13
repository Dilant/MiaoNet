namespace Celeste.Mod.MiaoNet;

public readonly record struct CommandArgumentInfo(
    int Start,
    int End,
    CommandSegmentType SegmentType,
    bool FreeText,
    bool IsSubcommandSelector,
    IReadOnlyList<string>? Subcommands,
    bool IsOverflow = false
)
{
    public int Length => End - Start;
}
