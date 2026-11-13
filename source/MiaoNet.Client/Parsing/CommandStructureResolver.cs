using MiaoNet.Shared;

namespace Celeste.Mod.MiaoNet;

/// <summary>
/// Resolves what a chat input names:
/// the command, and how the typed arguments map onto the segments that command declares.
/// Argument count is not validated.
/// </summary>
public static class CommandStructureResolver
{
    public const string CommandPrefix = "/";

    public static MiaoNetCommand? MatchCommand(IReadOnlyCollection<MiaoNetCommand> commands, string name)
    {
        foreach (MiaoNetCommand command in commands)
        {
            if (command.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return command;
            if (command.Aliases is not null)
            {
                foreach (string alias in command.Aliases)
                {
                    if (alias.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return command;
                }
            }
        }
        return null;
    }

    public static MiaoNetCommand? Resolve(
        string text,
        IReadOnlyCollection<MiaoNetCommand> commands,
        out int nameEnd,
        out IReadOnlyList<CommandArgumentInfo> arguments
    )
        => Resolve(text, commands, out nameEnd, out arguments, out _, out _);

    /// <summary>
    /// As the previous overload, also reporting the deepest syntax the arguments were resolved
    /// with and the index of the first argument that syntax governs. Subcommands descend, so this
    /// is not always <see cref="MiaoNetCommand.Syntax"/>.
    /// </summary>
    public static MiaoNetCommand? Resolve(
        string text,
        IReadOnlyCollection<MiaoNetCommand> commands,
        out int nameEnd,
        out IReadOnlyList<CommandArgumentInfo> arguments,
        out CommandSyntax? appliedSyntax,
        out int appliedStart
    )
    {
        if (text.Length == 0)
        {
            nameEnd = 0;
            arguments = Array.Empty<CommandArgumentInfo>();
            appliedSyntax = null;
            appliedStart = 0;
            return null;
        }

        SafeGuard.Assert(text.StartsWith(CommandPrefix, StringComparison.Ordinal));

        nameEnd = IndexOfWhitespace(text, CommandPrefix.Length);
        if (nameEnd == -1)
            nameEnd = text.Length;

        MiaoNetCommand? command = MatchCommand(commands, text[CommandPrefix.Length..nameEnd]);
        appliedSyntax = command?.Syntax;
        appliedStart = 0;
        arguments = command is null
            ? Array.Empty<CommandArgumentInfo>()
            : CommandSyntaxResolver.Resolve(text, nameEnd, command.Syntax, out appliedSyntax, out appliedStart);
        return command;
    }

    internal static int IndexOfWhitespace(string text, int start)
    {
        for (int i = start; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
                return i;
        }
        return -1;
    }
}
