using System.Collections;
using Celeste.Mod.MiaoNet;

namespace MiaoNet.UnitTest;

[TestClass]
public class CommandSyntaxTest
{
    private static CommandParser CreateGitParser()
    {
        CommandSyntax push = new([CommandSegmentType.Text], false);
        CommandSyntax add = new([CommandSegmentType.Text, CommandSegmentType.Text], false);
        CommandSyntax remove = new([CommandSegmentType.Text], false);
        CommandSyntax remote = new(
            [new CommandSegment(new Dictionary<string, CommandSyntax> { ["add"] = add, ["remove"] = remove })], false);
        CommandSyntax git = new(
            [new CommandSegment(new Dictionary<string, CommandSyntax> { ["push"] = push, ["remote"] = remote })], false);

        MiaoNetCommand command = new("git", null, [CommandSegmentType.CommandName], false, null!, git);
        return new CommandParser([command]);
    }

    private static IReadOnlyList<CommandArgumentInfo> Analyze(CommandParser parser, string text)
    {
        CommandStructureResolver.Resolve(text, parser.Commands, out _, out IReadOnlyList<CommandArgumentInfo> args);
        return args;
    }

    [TestMethod]
    public void Subcommand_SelectsNestedSyntax()
    {
        IReadOnlyList<CommandArgumentInfo> args = Analyze(CreateGitParser(), "/git push origin");

        Assert.HasCount(2, args);
        Assert.AreEqual(CommandSegmentType.CommandName, args[0].SegmentType);
        Assert.IsTrue(args[0].IsSubcommandSelector);
        Assert.IsFalse(args[0].FreeText);
        Assert.AreEqual(CommandSegmentType.Text, args[1].SegmentType);
        Assert.IsTrue(args[1].FreeText);
    }

    [TestMethod]
    public void Subcommand_NestsToDeeperArguments()
    {
        IReadOnlyList<CommandArgumentInfo> args = Analyze(CreateGitParser(), "/git remote add origin url");

        Assert.HasCount(4, args);
        Assert.AreEqual(CommandSegmentType.CommandName, args[0].SegmentType);
        Assert.IsTrue(args[0].IsSubcommandSelector);
        Assert.AreEqual(CommandSegmentType.CommandName, args[1].SegmentType);
        Assert.IsTrue(args[1].IsSubcommandSelector);
        Assert.AreEqual(CommandSegmentType.Text, args[2].SegmentType);
        Assert.AreEqual(CommandSegmentType.Text, args[3].SegmentType);
    }

    [TestMethod]
    public void Subcommand_IsCaseInsensitive()
    {
        IReadOnlyList<CommandArgumentInfo> args = Analyze(CreateGitParser(), "/git PUSH origin");

        Assert.HasCount(2, args);
        Assert.AreEqual(CommandSegmentType.Text, args[1].SegmentType);
        Assert.IsFalse(args[1].IsSubcommandSelector);
    }

    [TestMethod]
    public void UnknownSubcommand_FallsBackToTextAndTheRestOverflows()
    {
        IReadOnlyList<CommandArgumentInfo> args = Analyze(CreateGitParser(), "/git nope whatever");

        Assert.HasCount(2, args);
        Assert.IsTrue(args[0].IsSubcommandSelector);
        Assert.IsTrue(args[0].FreeText);
        CollectionAssert.Contains((System.Collections.ICollection)args[0].Subcommands!, "push");
        CollectionAssert.Contains((System.Collections.ICollection)args[0].Subcommands!, "remote");
        Assert.IsTrue(args[1].IsOverflow);
    }

    [TestMethod]
    public void Parse_SubcommandArguments_CountAgainstTheDescent()
    {
        // "/git push origin": push's own syntax has one Text slot, so this is valid even though
        // the top-level command declares a single segment.
        var parser = CreateGitParser();

        Assert.AreEqual(
            CommandParser.ParseResult.Success,
            parser.Parse("/git push origin", out _, out _, out var args));
        CollectionAssert.AreEqual(new[] { "push", "origin" }, (ICollection?)args);
    }

    [TestMethod]
    public void Parse_NestedSubcommandArguments_CountAgainstTheDescent()
    {
        var parser = CreateGitParser();

        Assert.AreEqual(
            CommandParser.ParseResult.Success,
            parser.Parse("/git remote add origin url", out _, out _, out var args));
        CollectionAssert.AreEqual(
            new[] { "remote", "add", "origin", "url" }, (ICollection?)args);
    }

    [TestMethod]
    public void Parse_SubcommandMissingArgument_IsReported()
    {
        var parser = CreateGitParser();

        Assert.AreEqual(
            CommandParser.ParseResult.MissingArguments,
            parser.Parse("/git push", out _, out _, out _));
    }

    [TestMethod]
    public void Parse_SubcommandTooManyArguments_IsReported()
    {
        var parser = CreateGitParser();

        Assert.AreEqual(
            CommandParser.ParseResult.TooManyArguments,
            parser.Parse("/git push origin extra", out _, out _, out _));
    }

    [TestMethod]
    public void Parse_NestedSubcommandTooManyArguments_IsReported()
    {
        var parser = CreateGitParser();

        Assert.AreEqual(
            CommandParser.ParseResult.TooManyArguments,
            parser.Parse("/git remote add a b c", out _, out _, out _));
    }

    [TestMethod]
    public void AppliedSyntax_ReportsTheDescent()
    {
        var parser = CreateGitParser();

        CommandStructureResolver.Resolve("/git remote add origin url", parser.Commands, out _, out var args,
            out CommandSyntax? applied, out int appliedStart);

        Assert.IsNotNull(applied);
        Assert.HasCount(2, applied.Segments);
        Assert.AreEqual(2, appliedStart);
        Assert.HasCount(4, args);
    }

    [TestMethod]
    public void AppliedSyntax_SelectorOnly_StaysTopLevel()
    {
        var parser = CreateGitParser();

        // "/git" with nothing after it never descends, so the selector's syntax applies as is.
        CommandStructureResolver.Resolve("/git", parser.Commands, out _, out _, out CommandSyntax? applied, out int appliedStart);

        Assert.AreEqual(0, appliedStart);
        Assert.HasCount(1, applied!.Segments);
        Assert.IsTrue(applied.Segments[0].IsSubcommand);
    }

    [TestMethod]
    public void AppliedSyntax_NamedSubcommand_ReportsItsSyntaxAndStart()
    {
        var parser = CreateGitParser();

        CommandStructureResolver.Resolve("/git push", parser.Commands, out _, out _, out CommandSyntax? applied, out int appliedStart);

        Assert.AreEqual(1, appliedStart);
        Assert.HasCount(1, applied!.Segments);
        Assert.IsFalse(applied.Segments[0].IsSubcommand);
    }
}
