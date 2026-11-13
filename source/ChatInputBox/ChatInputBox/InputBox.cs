using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.ChatInputBox;

public sealed class InputBox
{
    public const float CaretBlinkInterval = 0.5f;

    private readonly ILiteralTextRenderer textRenderer;
    private readonly ICompletionProvider completionProvider;
    private readonly IChatInputHighlighter? highlighter;

    private readonly TextBuffer buffer;
    private List<Completion>? completions;

    // With no runs the literal renderer draws every glyph in white, so this stays allocation-free.
    private static readonly IReadOnlyList<ColoredTextRange> PlainSpans = [];

    private bool showCaret = true;
    private float caretTimer = CaretBlinkInterval;

    private static readonly VirtualButton leftButton;
    private static readonly VirtualButton rightButton;
    private static readonly VirtualButton upButton;
    private static readonly VirtualButton downButton;

    private string? imeEditingText = null;
    private int imeEditingStart = 0;
    private int imeEditingLength = 0;

    private int selectedCompletionIndex = -1;

    // Index of the first visible candidate when the list is taller than the popup.
    private int completionScroll;

    // The provider version the current list was built from, so it refreshes when the
    // candidates themselves change (and not only when the text does).
    private int completionsVersion;

    private bool suppressCompletions;

    public string Text => buffer.Text;

    [MemberNotNullWhen(true, nameof(completions))]
    public bool HasCompletions => completions is { Count: > 0 };

    public int MaxTextLength { get; set; } = 64;

    static InputBox()
    {
        leftButton = new(new Binding() { Keyboard = [Keys.Left] }, Input.Gamepad, 0f, 0.4f);
        leftButton.SetRepeat(0.4f, 0.05f);

        rightButton = new(new Binding() { Keyboard = [Keys.Right] }, Input.Gamepad, 0f, 0.4f);
        rightButton.SetRepeat(0.4f, 0.05f);

        upButton = new(new Binding() { Keyboard = [Keys.Up] }, Input.Gamepad, 0f, 0.4f);
        upButton.SetRepeat(0.4f, 0.05f);

        downButton = new(new Binding() { Keyboard = [Keys.Down] }, Input.Gamepad, 0f, 0.4f);
        downButton.SetRepeat(0.4f, 0.05f);
    }

    public InputBox(ILiteralTextRenderer textRenderer, ICompletionProvider completionProvider, IChatInputHighlighter? highlighter = null)
    {
        this.textRenderer = textRenderer;
        this.completionProvider = completionProvider;
        this.highlighter = highlighter;

        buffer = new();
        buffer.TextOrCaretChanged += OnTextOrCaretChanged;
    }

    private void OnTextOrCaretChanged()
    {
        if (suppressCompletions)
        {
            suppressCompletions = false;
            completions = null;
            selectedCompletionIndex = -1;
            return;
        }
        RefreshCompletions();
    }

    private void RefreshCompletions()
    {
        completions = completionProvider.GetCompletions(buffer.Text, buffer.CaretPosition)?.ToList();
        completionsVersion = completionProvider.Version;
        selectedCompletionIndex = -1;
        completionScroll = 0;
    }

    public void Activate()
    {
        TextInput.OnInput += OnCharInput;
        TextInputEXT.TextEditing += TextInputEXT_TextEditing;
    }

    public void Deactivate()
    {
        TextInput.OnInput -= OnCharInput;
        TextInputEXT.TextEditing -= TextInputEXT_TextEditing;
        buffer.Clear();
        selectedCompletionIndex = -1;
    }

    public void SetText(string text)
    {
        buffer.SetText(text);
        SetAlwaysShowCaretTimer();
    }

    public void Update()
    {
        // The candidates can change without the text changing, so keep the open list current.
        if (completions is not null && completionProvider.Version != completionsVersion)
            RefreshCompletions();

        if (!MInput.Keyboard.CurrentState.IsKeyDown(Keys.LeftShift) &&
            !MInput.Keyboard.CurrentState.IsKeyDown(Keys.RightShift) &&
            rightButton.Pressed)
        {
            rightButton.ConsumePress();
            if (buffer.MoveCaretForward())
                SetAlwaysShowCaretTimer();
        }
        else if (!MInput.Keyboard.CurrentState.IsKeyDown(Keys.LeftShift) &&
                 !MInput.Keyboard.CurrentState.IsKeyDown(Keys.RightShift) &&
                 leftButton.Pressed)
        {
            leftButton.ConsumePress();
            if (buffer.MoveCaretBackward())
                SetAlwaysShowCaretTimer();
        }
        else if (upButton.Pressed)
        {
            if (HasCompletions)
            {
                upButton.ConsumePress();
                if (selectedCompletionIndex == -1)
                {
                    // Entering from the input lands on the last candidate, scrolling to it.
                    selectedCompletionIndex = completions.Count - 1;
                }
                else
                {
                    selectedCompletionIndex--;
                    if (selectedCompletionIndex == -1)
                        selectedCompletionIndex = completions.Count - 1;
                }
                ScrollSelectionIntoView();
            }
        }
        else if (downButton.Pressed)
        {
            if (HasCompletions)
            {
                downButton.ConsumePress();
                if (selectedCompletionIndex == -1)
                {
                    selectedCompletionIndex = 0;
                    completionScroll = 0;
                }
                else
                {
                    selectedCompletionIndex++;
                    selectedCompletionIndex %= completions.Count;
                }
                ScrollSelectionIntoView();
            }
        }
        else if (MInput.Keyboard.Pressed(Keys.Tab))
        {
            if (completions is not null && (completions.Count == 1 || selectedCompletionIndex != -1))
            {
                Completion selected = completions[selectedCompletionIndex == -1 ? 0 : selectedCompletionIndex];
                bool keepMenuClosed = selected.ShouldCloseMenu || !buffer.TextAfterCaret.All(char.IsWhiteSpace);
                if (keepMenuClosed)
                    SetSuppressCompletions();
                buffer.DoCompletion(selected.Remove, selected.RemoveRight, selected.Content, selected.Separator, MaxTextLength);
            }
        }

        bool ctrlPressing = MInput.Keyboard.Check(Keys.LeftControl) ||
            MInput.Keyboard.Check(Keys.RightControl);

        if (MInput.Keyboard.Pressed(Keys.V) && ctrlPressing)
        {
            string text = TextInput.GetClipboardText();
            string textNoControl = new string(text.Where(c => !char.IsControl(c)).ToArray());

            if (Text.Length + textNoControl.Length > MaxTextLength)
                textNoControl = textNoControl[..(MaxTextLength - Text.Length)];

            if (!string.IsNullOrEmpty(textNoControl))
                buffer.InputString(textNoControl);
        }

        if (caretTimer > 0f)
        {
            caretTimer -= Engine.RawDeltaTime;
        }
        else
        {
            caretTimer = CaretBlinkInterval;
            showCaret = !showCaret;
        }
    }

    private void OnCharInput(char chr)
    {
        bool operated = false;
        if (char.IsControl(chr))
        {
            switch (chr)
            {
            case (char)8: operated = buffer.Backspace(); break; // backspace
            case (char)2: operated = buffer.MoveCaretToHome(); break; // home
            case (char)3: operated = buffer.MoveCaretToEnd(); break; // end
            case (char)127: operated = buffer.Delete(); break; // delete
            }

        }
        else
        {
            // TODO need we support surrogate pair?

            if (textRenderer.CanRender(chr) && Text.Length < MaxTextLength)
            {
                buffer.InputChar(chr);
                operated = true;
            }
        }
        if (operated)
            SetAlwaysShowCaretTimer();
    }

    private void TextInputEXT_TextEditing(string? text, int start, int length)
    {
        imeEditingText = text;
        imeEditingStart = start;
        imeEditingLength = length;
    }

    private void SetAlwaysShowCaretTimer()
    {
        showCaret = true;
        caretTimer = CaretBlinkInterval;
    }

    // any better ways?
    public void SetSuppressCompletions()
    {
        suppressCompletions = true;
    }

    // Only LF starts a new line, matching MiaoNetFont's literal layout.
    private const char LineBreak = '\n';

    public void Render()
    {
        const float Margin = 16f;
        const float Padding = 8f;

        Vector2 baseLoc = new Vector2(Margin, Engine.Height - Margin);
        Vector2 textBaseLoc = baseLoc + new Vector2(Padding, -Padding);

        // Lay the input out exactly once per frame; measuring and every draw below reuse it.
        ILiteralTextLayout layout = textRenderer.LayoutLiteral(buffer.Text);
        int lineCount = layout.LineCount;
        float lineHeight = textRenderer.LineHeight;
        float height = lineCount * lineHeight + 2 * Padding;
        Draw.Rect(
            position: baseLoc - Vector2.UnitY * height,
            width: Engine.Width - 2 * Margin,
            height: height,
            color: Color.Black * (0x7f / 255f)
        );

        IReadOnlyList<ColoredTextRange> spans = highlighter?.Highlight(buffer.Text, buffer.CaretPosition)
            ?? PlainSpans;

        // Anchor the last line to the bottom of the box, so extra lines grow upward.
        float literalY = textBaseLoc.Y - lineCount * lineHeight;
        Vector2 LineOrigin(int line) => new(textBaseLoc.X, literalY + line * lineHeight);

        int caretLine = 0;
        int caretLineStart = 0;
        for (int i = 0; i < buffer.CaretPosition; i++)
            if (buffer.Text[i] == LineBreak)
            {
                caretLine++;
                caretLineStart = i + 1;
            }

        float caretInlineWidth = layout.MeasurePrefix(caretLineStart, buffer.CaretPosition);
        float caretLineTop = literalY + caretLine * lineHeight;
        float caretWidth = caretInlineWidth;

        Vector2 sizeImeEditing = Vector2.Zero;
        if (imeEditingText is not null)
        {
            layout.Draw(LineOrigin(0), 0, buffer.CaretPosition, 0f, spans);

            ILiteralTextLayout imeLayout = textRenderer.LayoutLiteral(imeEditingText);
            sizeImeEditing = imeLayout.Size;
            Vector2 imePos = new Vector2(textBaseLoc.X + caretInlineWidth, caretLineTop);
            imeLayout.Draw(imePos, 0, imeEditingText.Length, 0f, Color.Gray);
            caretWidth += imeLayout.MeasurePrefix(
                0, Math.Min(imeEditingStart, imeEditingText.Length));

            int nextBreak = buffer.Text.IndexOf(LineBreak, buffer.CaretPosition);
            int lineEnd = nextBreak < 0 ? buffer.Text.Length : nextBreak;
            layout.Draw(
                LineOrigin(caretLine), buffer.CaretPosition, lineEnd,
                sizeImeEditing.X, spans
            );
            if (nextBreak >= 0)
                layout.Draw(
                    LineOrigin(caretLine + 1), nextBreak + 1, buffer.Text.Length, 0f, spans
                );
        }
        else
        {
            layout.Draw(LineOrigin(0), 0, buffer.Text.Length, 0f, spans);
        }

        if (showCaret)
        {
            Vector2 fromLoc = new Vector2(textBaseLoc.X + caretWidth, caretLineTop + lineHeight);
            Vector2 toLoc = fromLoc - new Vector2(0f, lineHeight);

            Draw.Line(fromLoc, toLoc, Color.White, 2f);
        }

        {
            Vector2 view = new(Engine.ViewWidth, Engine.ViewHeight);
            float xScale = view.X / Engine.Width;
            float yScale = view.Y / Engine.Height;
            Vector2 viewPos = new Vector2(Engine.Viewport.X, Engine.Viewport.Y) +
                new Vector2((textBaseLoc.X + caretInlineWidth) * xScale, caretLineTop * yScale);
            Rectangle finalRect = new Rectangle(
                (int)viewPos.X,
                (int)viewPos.Y,
                Math.Max(1, (int)(sizeImeEditing.X * xScale)),
                (int)(lineHeight * yScale)
            );
            // Known issue: with only a few pinyin letters typed, the candidate window covers
            // the input row, and it only avoids it once more input arrives. A plain SDL2-only
            // project shows the same thing, so this is probably an SDL2-side issue.
            TextInputEXT.SetInputRectangle(finalRect);
        }

        if (HasCompletions)
        {
            const float CompletionsPadding = 4f;
            int count = completions.Count;
            int visibleCount = Math.Min(count, MaxVisibleCompletions);
            int scroll = Math.Clamp(completionScroll, 0, count - visibleCount);
            completionScroll = scroll;
            bool hasAbove = scroll > 0;
            bool hasBelow = scroll + visibleCount < count;

            Vector2 cBaseLoc = new Vector2(textBaseLoc.X + caretWidth, caretLineTop - Padding);
            Vector2 cTextBaseLoc = cBaseLoc + new Vector2(CompletionsPadding, -CompletionsPadding);

            // Lay the visible candidates out once per frame; the width and the draw below reuse it.
            ILiteralTextLayout[] completionLayouts = new ILiteralTextLayout[visibleCount];
            float width = 0f;
            for (int i = 0; i < visibleCount; i++)
            {
                completionLayouts[i] = textRenderer.LayoutLiteral(completions[scroll + i].Display);
                width = Math.Max(width, completionLayouts[i].Size.X);
            }
            if (hasAbove || hasBelow)
                width = Math.Max(width, lineHeight);

            int rowCount = visibleCount + (hasAbove ? 1 : 0) + (hasBelow ? 1 : 0);
            float totalHeight = lineHeight * rowCount;
            float cX = cBaseLoc.X;
            float cY = cBaseLoc.Y - totalHeight - CompletionsPadding * 2f;
            float cW = width + CompletionsPadding * 2f;
            float cH = totalHeight + CompletionsPadding * 2f;
            Draw.Rect(cX, cY, cW, cH, Color.Black * (0xaa / 255f));
            Draw.Rect(cX, cY, cW, 1f, Color.Cyan);
            Draw.Rect(cX - 3f, cY, 3f, cH, Color.CornflowerBlue);

            // Rows are drawn bottom-up, matching how the box grows away from the caret.
            float curY = cTextBaseLoc.Y;
            float arrowX = cX + cW / 2f;

            if (hasBelow)
            {
                DrawCompletionArrow(new Vector2(arrowX, curY - lineHeight / 2f), up: false, lineHeight);
                curY -= lineHeight;
            }

            for (int i = visibleCount - 1; i >= 0; i--)
            {
                int index = scroll + i;
                bool selected = index == selectedCompletionIndex;
                Color c = selected ? Color.White : Color.LightGray;
                if (selected)
                {
                    float sX = cBaseLoc.X;
                    float sY = curY - lineHeight;
                    float sW = cW;
                    float sH = lineHeight;
                    Draw.Rect(sX, sY, sW, sH, Color.Wheat * (0x22 / 255f));
                    Draw.Rect(sX - 3f, sY, 3f, sH, Color.Wheat);
                }

                Completion item = completions[index];
                Vector2 itemPos = new(cTextBaseLoc.X, curY - lineHeight);
                completionLayouts[i].Draw(itemPos, 0, item.Display.Length, 0f, CompletionRuns(item, c));
                curY -= lineHeight;
            }

            if (hasAbove)
                DrawCompletionArrow(new Vector2(arrowX, curY - lineHeight / 2f), up: true, lineHeight);
        }
    }

    private const int MaxVisibleCompletions = 15;

    private static readonly Color CompletionMatchColor = Color.Cyan;

    // A chevron drawn from lines, so it never depends on the font carrying an arrow glyph.
    private static void DrawCompletionArrow(Vector2 center, bool up, float rowHeight)
    {
        float half = rowHeight * 0.25f;
        float direction = up ? -1f : 1f;
        Vector2 apex = center + new Vector2(0f, direction * half);
        Vector2 left = center + new Vector2(-half, -direction * half);
        Vector2 right = center + new Vector2(half, -direction * half);

        Draw.Line(left, apex, Color.Cyan, 2f);
        Draw.Line(apex, right, Color.Cyan, 2f);
    }

    // Keeps the selection inside the visible window as it moves.
    private void ScrollSelectionIntoView()
    {
        if (selectedCompletionIndex < 0)
            return;

        if (selectedCompletionIndex < completionScroll)
            completionScroll = selectedCompletionIndex;
        else if (selectedCompletionIndex >= completionScroll + MaxVisibleCompletions)
            completionScroll = selectedCompletionIndex - MaxVisibleCompletions + 1;
    }

    // Paints the gaps between matches in the row colour and the matches themselves differently.
    private static IReadOnlyList<ColoredTextRange> CompletionRuns(Completion item, Color textColor)
    {
        if (item.Matches.Count == 0)
            return [new ColoredTextRange(0, item.Display.Length, textColor)];

        List<ColoredTextRange> runs = [];
        int cursor = 0;
        foreach (TextRange match in item.Matches)
        {
            if (match.Start > cursor)
                runs.Add(new ColoredTextRange(cursor, match.Start - cursor, textColor));
            runs.Add(new ColoredTextRange(match.Start, match.Length, CompletionMatchColor));
            cursor = match.End;
        }
        if (cursor < item.Display.Length)
            runs.Add(new ColoredTextRange(cursor, item.Display.Length - cursor, textColor));
        return runs;
    }
}
