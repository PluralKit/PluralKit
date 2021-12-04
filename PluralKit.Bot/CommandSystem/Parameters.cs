namespace PluralKit.Bot;

public class Parameters
{
    // Dictionary of (left, right) quote pairs
    // Each char in the string is an individual quote, multi-char strings imply "one of the following chars"
    private static readonly Dictionary<string, string> _quotePairs = new()
    {
        // Basic
        { "'", "'" }, // ASCII single quotes
        { "\"", "\"" }, // ASCII double quotes

        // "Smart quotes"
        // Specifically ignore the left/right status of the quotes and match any combination of them
        // Left string also includes "low" quotes to allow for the low-high style used in some locales
        { "\u201C\u201D\u201F\u201E", "\u201C\u201D\u201F" }, // double quotes
        { "\u2018\u2019\u201B\u201A", "\u2018\u2019\u201B" }, // single quotes

        // Chevrons (normal and "fullwidth" variants)
        { "\u00AB\u300A", "\u00BB\u300B" }, // double chevrons, pointing away (<<text>>)
        { "\u00BB\u300B", "\u00AA\u300A" }, // double chevrons, pointing together (>>text<<)
        { "\u2039\u3008", "\u203A\u3009" }, // single chevrons, pointing away (<text>)
        { "\u203A\u3009", "\u2039\u3008" }, // single chevrons, pointing together (>text<)

        // Other
        { "\u300C\u300E", "\u300D\u300F" } // corner brackets (Japanese/Chinese)
    };

    private ISet<string> _flags; // Only parsed when requested first time
    public int _ptr;

    public string FullCommand { get; }

    private struct WordPosition
    {
        // Start of the word
        internal readonly int startPos;

        // End of the word
        internal readonly int endPos;

        // How much to advance word pointer afterwards to point at the start of the *next* word
        internal readonly int advanceAfterWord;

        internal readonly bool wasQuoted;

        public WordPosition(int startPos, int endPos, int advanceAfterWord, bool wasQuoted)
        {
            this.startPos = startPos;
            this.endPos = endPos;
            this.advanceAfterWord = advanceAfterWord;
            this.wasQuoted = wasQuoted;
        }
    }

    public Parameters(string cmd)
    {
        // This is a SUPER dirty hack to avoid having to match both spaces and newlines in the word detection below
        // Instead, we just add a space before every newline (which then gets stripped out later).
        FullCommand = cmd.Replace("\n", " \n");
        _ptr = 0;
    }

    private void ParseFlags()
    {
        _flags = new HashSet<string>();

        var ptr = 0;
        while (NextWordPosition(ptr) is { } wp)
        {
            ptr = wp.endPos + wp.advanceAfterWord;

            // Is this word a *flag* (as in, starts with a - AND is not quoted)
            if (FullCommand[wp.startPos] != '-' || wp.wasQuoted) continue; // (if not, carry on w/ next word)

            // Find the *end* of the flag start (technically allowing arbitrary amounts of dashes)
            var flagNameStart = wp.startPos;
            while (flagNameStart < FullCommand.Length && FullCommand[flagNameStart] == '-')
                flagNameStart++;

            // Then add the word to the flag set
            var word = FullCommand.Substring(flagNameStart, wp.endPos - flagNameStart).Trim();
            if (word.Length > 0)
                _flags.Add(word.ToLowerInvariant());
        }
    }

    public string Pop()
    {
        // Loop to ignore and skip past flags
        while (NextWordPosition(_ptr) is { } pos)
        {
            _ptr = pos.endPos + pos.advanceAfterWord;
            if (FullCommand[pos.startPos] == '-' && !pos.wasQuoted) continue;
            return FullCommand.Substring(pos.startPos, pos.endPos - pos.startPos).Trim();
        }

        return "";
    }

    public string Peek()
    {
        // temp ptr so we don't move the real ptr
        int ptr = _ptr;

        return PeekWithPtr(ref ptr);
    }

    public string PeekWithPtr(ref int ptr)
    {
        // Loop to ignore and skip past flags
        while (NextWordPosition(ptr) is { } pos)
        {
            ptr = pos.endPos + pos.advanceAfterWord;
            if (FullCommand[pos.startPos] == '-' && !pos.wasQuoted) continue;
            return FullCommand.Substring(pos.startPos, pos.endPos - pos.startPos).Trim();
        }

        return "";
    }

    public ISet<string> Flags()
    {
        if (_flags == null) ParseFlags();
        return _flags;
    }

    public string Remainder(bool skipFlags = true)
    {
        if (skipFlags)
            // Skip all *leading* flags when taking the remainder
            while (NextWordPosition(_ptr) is { } wp)
            {
                if (FullCommand[wp.startPos] != '-' || wp.wasQuoted) break;
                _ptr = wp.endPos + wp.advanceAfterWord;
            }

        // *Then* get the remainder
        return FullCommand.Substring(Math.Min(_ptr, FullCommand.Length)).Trim();
    }

    private WordPosition? NextWordPosition(int position)
    {
        // Skip leading spaces before actual content
        while (position < FullCommand.Length && FullCommand[position] == ' ') position++;

        // Is this the end of the string?
        if (FullCommand.Length <= position) return null;

        // Is this a quoted word?
        if (TryCheckQuote(FullCommand[position], out var endQuotes))
        {
            // We found a quoted word - find an instance of one of the corresponding end quotes
            var endQuotePosition = -1;
            for (var i = position + 1; i < FullCommand.Length; i++)
                if (endQuotePosition == -1 && endQuotes.Contains(FullCommand[i]))
                    endQuotePosition = i; // need a break; don't feel like brackets tho lol

            // Position after the end quote should be EOL or a space
            // Otherwise we fallthrough to the unquoted word handler below
            if (FullCommand.Length == endQuotePosition + 1 || FullCommand[endQuotePosition + 1] == ' ')
                return new WordPosition(position + 1, endQuotePosition, 2, true);
        }

        // Not a quoted word, just find the next space and return if it's the end of the command
        var wordEnd = FullCommand.IndexOf(' ', position + 1);

        return wordEnd == -1
            ? new WordPosition(position, FullCommand.Length, 0, false)
            : new WordPosition(position, wordEnd, 1, false);
    }

    private bool TryCheckQuote(char potentialLeftQuote, out string correspondingRightQuotes)
    {
        foreach (var (left, right) in _quotePairs)
            if (left.Contains(potentialLeftQuote))
            {
                correspondingRightQuotes = right;
                return true;
            }

        correspondingRightQuotes = null;
        return false;
    }
}