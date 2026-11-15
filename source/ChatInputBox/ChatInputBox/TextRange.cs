namespace Celeste.Mod.ChatInputBox;

public readonly struct TextRange
{
    public int Start { get; }

    public int Length { get; }

    public TextRange(int start, int length)
    {
        Start = start;
        Length = length;
    }

    public int End => Start + Length;

    /// <summary>Every non-overlapping occurrence of <paramref name="needle"/>, case-insensitively.</summary>
    public static IReadOnlyList<TextRange> FindAll(string text, string? needle)
    {
        if (string.IsNullOrEmpty(needle))
            return [];

        List<TextRange> ranges = [];
        int index = 0;
        while (index <= text.Length - needle.Length)
        {
            int found = text.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
                break;
            ranges.Add(new TextRange(found, needle.Length));
            index = found + needle.Length;
        }
        return ranges;
    }
}
