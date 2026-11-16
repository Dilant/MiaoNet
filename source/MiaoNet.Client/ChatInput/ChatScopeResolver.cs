using Celeste.Mod.ChatInputBox;
using MiaoNet.Shared;

namespace Celeste.Mod.MiaoNet;

/// <summary>Works out which chat domain a line of input will end up in.</summary>
public static class ChatScopeResolver
{
    /// <summary>
    /// The channel whose members a mention in <paramref name="text"/> can reach, or
    /// <see langword="null"/> when the line does not send chat at all and therefore
    /// mentions nobody.
    /// </summary>
    public static ChatChannel? ResolveMentionReach(string text, CommandParser parser, ChatChannel current)
    {
        if (text.Length == 0 || text[0] != CommandStructureResolver.CommandPrefix[0])
            return current;

        int nameEnd = text.Length;
        for (int i = 1; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                nameEnd = i;
                break;
            }
        }

        MiaoNetCommand? command = CommandStructureResolver.MatchCommand(parser.Commands, text[1..nameEnd]);
        if (command is null)
            return null;

        return command.ChatSendScope switch
        {
            ChatSendScope.Current => current,
            ChatSendScope.Global => ChatChannel.Global,
            ChatSendScope.Channel => ChatChannel.Channel,
            ChatSendScope.Map => ChatChannel.Map,
            _ => null,
        };
    }
}
