using Celeste.Mod.ChatInputBox;
using MiaoNet.Shared;

namespace Celeste.Mod.MiaoNet;

public sealed class ChatCompletionProvider : ICompletionProvider
{
    private const StringComparison sc = StringComparison.OrdinalIgnoreCase;

    private readonly MiaoNetContext context;
    private readonly CommandParser parser;
    private readonly IChatInputState inputState;

    public ChatCompletionProvider(MiaoNetContext context, CommandParser parser, IChatInputState inputState)
    {
        this.context = context;
        this.parser = parser;
        this.inputState = inputState;
    }

    // The roster version alone misses a chat-channel switch, which changes the mention scope.
    public int Version => HashCode.Combine(inputState.Version, inputState.ChatChannel);

    public IEnumerable<Completion>? GetCompletions(string text, int caret)
    {
        if (context.ClientState is null)
            return null;

        string before = text[..caret];

        // Command arguments complete from the raw input: Emoji.Apply would mangle a typed value
        // that looks like ":emoji:" (e.g. a player named ":glad:glad:glad:") and hide its candidates.
        IEnumerable<Completion>? completions = GetCommandCompletions(text, caret);
        if (completions is not null)
            return completions;

        // A "@..." word completes players, and may at the same time be an emoji context (a
        // following ":"), in which case both are offered with players first.
        ChatChannel? reach = ChatScopeResolver.ResolveMentionReach(text, parser, inputState.ChatChannel);
        IEnumerable<Completion>? mentions = GetMentionCompletions(before, reach);
        IEnumerable<Completion>? emojis = GetEmojiCompletions(Emoji.Apply(before));

        if (mentions is not null && emojis is not null)
            return mentions.Concat(emojis);
        return mentions ?? emojis;
    }

    private IEnumerable<Completion>? GetMentionCompletions(string input, ChatChannel? reach)
    {
        int atIndex = FindMentionStartAtEnd(input);
        if (atIndex == -1)
            return null;

        string partial = input[(atIndex + 1)..];
        int remove = partial.Length;
        return from name in inputState.MentionablePlayerNames(reach)
               where name.Contains(partial, sc)
               select new Completion(name, name, remove, match: partial);

        int FindMentionStartAtEnd(string text)
        {
            int start = -1;
            foreach (ChatInputToken token in ChatLexer.Lex(text, parser.Commands, static _ => false).Tokens)
            {
                if (token.Type == ChatInputTokenType.Mention && token.End == text.Length)
                    start = token.Start;
            }

            // A bare '@' is not a token yet, but it already starts the word being typed.
            if (start < 0 && text.Length > 0 && MentionBoundary.IsMentionStart(text, text.Length - 1))
                return text.Length - 1;
            return start;
        }
    }

    private static IEnumerable<Completion>? GetEmojiCompletions(string input)
    {
        int lastColonIndex = input.LastIndexOf(':');
        if (lastColonIndex == -1)
            return null;

        string afterColon = input[(lastColonIndex + 1)..];
        if (!afterColon.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            return null;

        int remove = input.Length - lastColonIndex - 1;
        return from e in Emoji.Registered
               where !e.StartsWith('\0')
               where e.Contains(afterColon, StringComparison.OrdinalIgnoreCase)
               select new Completion(
                   e, $"{(char)(Emoji.Get(e) + Emoji.Start)} {e}", remove,
                   ':', shouldCloseMenu: true, match: afterColon
               );
    }

    private IEnumerable<Completion>? GetCommandCompletions(string text, int caret)
    {
        if (text.Length == 0 || text[0] != '/')
            return null;

        int nameEnd = CommandStructureResolver.IndexOfWhitespace(text, 1);
        if (nameEnd == -1)
            nameEnd = text.Length;

        // The caret can be inside the command name, in which case the text after it belongs to
        // the same name and is replaced along with the part before it.
        if (caret <= nameEnd)
            return CommandNameCompletion.Complete(parser.Commands, text[1..caret], text[caret..nameEnd], nameEnd - caret);

        string input = text[..caret];
        bool endsWithSpace = input.Length > 0 && char.IsWhiteSpace(input[^1]);
        LexedChatInput lexed = ChatLexer.Lex(input, parser.Commands, static _ => false);
        if (lexed.Command is null)
            return null;

        int index = endsWithSpace ? lexed.Arguments.Count : lexed.Arguments.Count - 1;
        if (index < 0)
            return null;
        int remove = endsWithSpace ? 0 : lexed.Arguments[index].Length;

        // Bound by the applied syntax, not the command's top-level Segments: a subcommand
        // descent moves the governing slots further down.
        CommandSegment? segment = lexed.SegmentAt(index);
        if (segment is null)
            return []; // an argument beyond the applied segments: no completion

        CommandSegmentType segmentType = segment.Type;
        string part = endsWithSpace
            ? string.Empty
            : input[lexed.Arguments[index].Start..lexed.Arguments[index].End];

        // A slot that selects a subcommand completes the declared subcommand names rather than
        // top-level command names.
        IReadOnlyList<string>? subcommands = null;
        if (endsWithSpace)
            subcommands = segment.Subcommands?.Keys.ToList();
        else if (index < lexed.Arguments.Count)
            subcommands = lexed.Arguments[index].Subcommands;
        if (subcommands is not null)
            return GetSubcommandNameCompletions(subcommands, part, remove);

        var state = context.ClientState!;

        switch (segmentType)
        {
        case CommandSegmentType.Player:
        case CommandSegmentType.PlayerSameChannel:
        case CommandSegmentType.PlayerSameMap:
            return GetPlayerNameCompletions(
                CommandSegmentResolver.ResolvePlayers(state, segmentType.ToPlayerScope()), part);
        case CommandSegmentType.Channel:
        case CommandSegmentType.ChannelOrNew:
            return from c in CommandSegmentResolver.ResolveChannels(state)
                   let name = c.Info.Name
                   where name.Contains(part, sc)
                   select new Completion(name, name, remove, match: part);
        case CommandSegmentType.CommandName:
            return CommandNameCompletion.Complete(parser.Commands, part, string.Empty, 0);
        case CommandSegmentType.ChatChannelType:
            return from n in ChatChannelMatcher.Names
                   where n.Contains(part, sc)
                   select new Completion(n, n, remove, match: part);
        default:
            return null;
        }
    }

    private static IEnumerable<Completion> GetSubcommandNameCompletions(
        IReadOnlyList<string> names, string part, int remove)
        => from name in names
           where name.Contains(part, sc)
           select new Completion(name, name, remove);

    private static IEnumerable<Completion>? GetPlayerNameCompletions(IEnumerable<OnlinePlayer> players, string part)
        => from p in players
           let i = p.Info
           where i.Name.Contains(part, sc)
           select new Completion(i.Name, i.DisplayName, part.Length, match: part);
}
