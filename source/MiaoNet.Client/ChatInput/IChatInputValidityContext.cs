using ChatChannel = MiaoNet.Shared.ChatChannel;

namespace Celeste.Mod.MiaoNet;

public interface IChatInputValidityContext
{
    public ChatInputValidity CheckCommand(string name);

    public ChatInputValidity CheckPlayer(string name, PlayerScope scope);

    /// <param name="reach">The channel the line will be sent to, or null when it is not chat.</param>
    public ChatInputValidity CheckMention(string name, ChatChannel? reach);

    public ChatInputValidity CheckChannel(string name);

    public ChatInputValidity CheckChannelType(string name);

    public ChatInputValidity CheckEmoji(string name);

    // Whether some valid value still contains the typed text, i.e. whether any completion is left.
    // Once this is false, no further typing can make the token valid.

    public bool CanStillCompleteCommand(string partial);

    public bool CanStillCompletePlayer(string partial, PlayerScope scope);

    public bool CanStillCompleteMention(string partial, ChatChannel? reach);

    public bool CanStillCompleteChannel(string partial);

    public bool CanStillCompleteChannelType(string partial);
}
