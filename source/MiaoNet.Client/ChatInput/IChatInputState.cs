using ChatChannel = MiaoNet.Shared.ChatChannel;

namespace Celeste.Mod.MiaoNet;

public interface IChatInputState
{
    public bool IsConnected { get; }

    public int Version { get; }

    /// <summary>The chat domain plain text is currently sent to.</summary>
    public ChatChannel ChatChannel { get; }

    public IReadOnlyCollection<string> AllPlayerNames { get; }

    public IReadOnlyCollection<string> PlayerNamesInScope(PlayerScope scope);

    /// <summary>
    /// Names of the players that receive text sent to <paramref name="reach"/>, since only
    /// they can be notified by a mention in it. <see langword="null"/> means the line is not
    /// chat at all, where every known name is offered.
    /// </summary>
    public IReadOnlyCollection<string> MentionablePlayerNames(ChatChannel? reach);

    public IReadOnlyCollection<string> PublicChannelNames { get; }
}
