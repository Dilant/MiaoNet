namespace Celeste.Mod.ChatInputBox;

public readonly struct Completion
{
    public string Display { get; }

    public string Content { get; }

    public char Separator { get; }

    public bool ShouldCloseMenu { get; }

    public int Remove { get; }

    /// <summary>How many characters right of the caret the completion also replaces.</summary>
    public int RemoveRight { get; }

    /// <summary>Parts of <see cref="Display"/> that matched the typed text.</summary>
    public IReadOnlyList<TextRange> Matches { get; }

    public Completion(
        string content, string display, int remove, char separator = ' ', bool shouldCloseMenu = false,
        int removeRight = 0, string? match = null
    )
    {
        Content = content;
        Display = display;
        Separator = separator;
        ShouldCloseMenu = shouldCloseMenu;
        Remove = remove;
        RemoveRight = removeRight;
        Matches = TextRange.FindAll(display, match);
    }
}
