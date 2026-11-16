using Celeste.Mod.MiaoNet;
using MiaoNet.Shared;

namespace MiaoNet.UnitTest;

[TestClass]
public class ChatScopeResolverTest
{
    private static readonly MiaoNetCommand CmdSay =
        new("say", null, [CommandSegmentType.Text], true, null!, chatSendScope: ChatSendScope.Current);
    private static readonly MiaoNetCommand CmdGlobalChat =
        new("global-chat", ["gc"], [CommandSegmentType.Text], true, null!, chatSendScope: ChatSendScope.Global);
    private static readonly MiaoNetCommand CmdChannelChat =
        new("channel-chat", ["cc"], [CommandSegmentType.Text], true, null!, chatSendScope: ChatSendScope.Channel);
    private static readonly MiaoNetCommand CmdMapChat =
        new("map-chat", ["mc"], [CommandSegmentType.Text], true, null!, chatSendScope: ChatSendScope.Map);
    private static readonly MiaoNetCommand CmdWhisper =
        new("whisper", ["w"], [CommandSegmentType.Player, CommandSegmentType.Text], true, null!);

    private static readonly CommandParser Parser =
        new([CmdSay, CmdGlobalChat, CmdChannelChat, CmdMapChat, CmdWhisper]);

    private static ChatChannel? Resolve(string text, ChatChannel current = ChatChannel.Global)
        => ChatScopeResolver.ResolveMentionReach(text, Parser, current);

    [TestMethod]
    public void PlainText_UsesTheSelectedChannel()
    {
        Assert.AreEqual(ChatChannel.Map, Resolve("hi @bob", ChatChannel.Map));
        Assert.AreEqual(ChatChannel.Channel, Resolve("hi @bob", ChatChannel.Channel));
    }

    [TestMethod]
    public void ChatCommands_OverrideTheSelectedChannel()
    {
        Assert.AreEqual(ChatChannel.Global, Resolve("/gc hi @bob", ChatChannel.Map));
        Assert.AreEqual(ChatChannel.Channel, Resolve("/cc hi @bob", ChatChannel.Map));
        Assert.AreEqual(ChatChannel.Map, Resolve("/mc hi @bob", ChatChannel.Global));
    }

    [TestMethod]
    public void AliasesAreRecognised()
    {
        Assert.AreEqual(ChatChannel.Map, Resolve("/mc hi", ChatChannel.Global));
        Assert.AreEqual(ChatChannel.Channel, Resolve("/cc hi", ChatChannel.Global));
    }

    [TestMethod]
    public void Say_FollowsTheSelectedChannel()
    {
        Assert.AreEqual(ChatChannel.Map, Resolve("/say hi @bob", ChatChannel.Map));
    }

    [TestMethod]
    public void NonChatCommands_MentionNobody()
    {
        Assert.IsNull(Resolve("/w bob hi @bob"));
        Assert.IsNull(Resolve("/unknown hi @bob"));
        Assert.IsNull(Resolve("/"));
    }

    [TestMethod]
    public void CommandNameIsCaseInsensitive()
    {
        Assert.AreEqual(ChatChannel.Map, Resolve("/MC hi", ChatChannel.Global));
    }
}
