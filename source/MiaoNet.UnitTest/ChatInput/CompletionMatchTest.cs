using Celeste.Mod.ChatInputBox;

namespace MiaoNet.UnitTest;

[TestClass]
public class CompletionMatchTest
{
    private static (int Start, int Length)[] Ranges(Completion completion)
        => completion.Matches.Select(m => (m.Start, m.Length)).ToArray();

    [TestMethod]
    public void MatchIsCaseInsensitive()
    {
        var completion = new Completion("unwatch", "unwatch (uw, uwt)", 1, match: "W");
        CollectionAssert.AreEqual(
            new[] { (2, 1), (10, 1), (14, 1) },
            Ranges(completion)
        );
    }

    [TestMethod]
    public void EmptyMatchHasNoRanges()
    {
        Assert.IsEmpty(new Completion("abc", "abc", 0).Matches);
        Assert.IsEmpty(new Completion("abc", "abc", 0, match: "").Matches);
    }

    [TestMethod]
    public void AllOccurrencesAreHighlighted()
    {
        var completion = new Completion("bob", "[p] bob", 1, match: "b");
        CollectionAssert.AreEqual(
            new[] { (4, 1), (6, 1) },
            Ranges(completion)
        );
    }

    [TestMethod]
    public void MatchLongerThanDisplayIsIgnored()
    {
        Assert.IsEmpty(new Completion("abc", "ab", 0, match: "abc").Matches);
    }
}
