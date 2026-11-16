using Celeste.Mod.ChatInputBox;

namespace Celeste.Mod.ChatInputBoxExample;

public class TestCompletionProvider : ICompletionProvider
{
    public int Version => 0;

    public IEnumerable<Completion> GetCompletions(string input)
    {
        return ((List<string>)["some", "completions", "here"]).Select(s => new Completion(s, s, 0));
    }
}
