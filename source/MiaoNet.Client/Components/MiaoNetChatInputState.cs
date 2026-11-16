using MiaoNet.Shared;

namespace Celeste.Mod.MiaoNet;

internal sealed class MiaoNetChatInputState : IChatInputState
{
    private readonly MiaoNetContext context;

    private int snapshotVersion = -1;
    private IReadOnlyCollection<string> allPlayerNames = [];
    private IReadOnlyCollection<string> publicChannelNames = [];
    private readonly Dictionary<PlayerScope, IReadOnlyCollection<string>> playerNamesInScope = [];
    private readonly Dictionary<ChatChannel, IReadOnlyCollection<string>> mentionablePlayerNames = [];

    public MiaoNetChatInputState(MiaoNetContext context)
    {
        this.context = context;
    }

    private ClientState? State => context.ClientState;

    public bool IsConnected => State is not null;

    public int Version => State?.Version ?? 0;

    public ChatChannel ChatChannel => MiaoNetModule.Settings.ChatChannel;

    public IReadOnlyCollection<string> AllPlayerNames
    {
        get
        {
            Refresh();
            return allPlayerNames;
        }
    }

    public IReadOnlyCollection<string> PublicChannelNames
    {
        get
        {
            Refresh();
            return publicChannelNames;
        }
    }

    public IReadOnlyCollection<string> PlayerNamesInScope(PlayerScope scope)
    {
        Refresh();
        if (playerNamesInScope.TryGetValue(scope, out IReadOnlyCollection<string>? cached))
            return cached;

        IReadOnlyCollection<string> names = State is { } state
            ? CommandSegmentResolver.ResolvePlayers(state, scope).Select(p => p.Info.Name).ToArray()
            : [];
        playerNamesInScope[scope] = names;
        return names;
    }

    // Mirrors the server's recipient filter for a chat channel, so the names offered are
    // exactly the players that would receive, and therefore be notified by, the message.
    public IReadOnlyCollection<string> MentionablePlayerNames(ChatChannel? reach)
    {
        Refresh();
        if (reach is not { } channel)
            return allPlayerNames;

        if (mentionablePlayerNames.TryGetValue(channel, out IReadOnlyCollection<string>? cached))
            return cached;

        IReadOnlyCollection<string> names = State is { } state
            ? MentionableIn(state, channel)
            : [];
        mentionablePlayerNames[channel] = names;
        return names;

        static IReadOnlyCollection<string> MentionableIn(ClientState state, ChatChannel channel)
        {
            if (channel == ChatChannel.Global)
                return state.AllPlayers.Select(p => p.Info.Name).ToArray();

            IEnumerable<OnlinePlayer> players = state.SelfChannel.Players;
            if (channel == ChatChannel.Map)
                players = players.Where(p => p.Location.Map == state.Self.Location.Map);

            return players.Select(p => p.Info.Name).Append(state.Self.Info.Name).ToArray();
        }
    }

    private void Refresh()
    {
        int version = Version;
        if (version == snapshotVersion)
            return;

        snapshotVersion = version;
        playerNamesInScope.Clear();
        mentionablePlayerNames.Clear();

        if (State is { } state)
        {
            allPlayerNames = state.AllPlayers.Select(p => p.Info.Name).ToArray();
            publicChannelNames = CommandSegmentResolver.ResolveChannels(state).Select(c => c.Info.Name).ToArray();
        }
        else
        {
            allPlayerNames = [];
            publicChannelNames = [];
        }
    }
}
