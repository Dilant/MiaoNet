#nullable enable

namespace Celeste.Mod.ChatInputBox;

// TODO using ReadOnlySpan<char>?
public interface ICompletionProvider
{
    /// <summary>
    /// Changes when the candidates themselves change, so an open list can refresh even
    /// though the text did not (e.g. a player joined the channel).
    /// </summary>
    public int Version { get; }

    public IEnumerable<Completion>? GetCompletions(string input);
}
