using System;
using System.Collections.Generic;

namespace PluralKit.Bot
{
    public class Parameters
    {
        private static readonly Dictionary<char, char> _quotePairs = new Dictionary<char, char>()
        {
            {'\'', '\''}, {'"', '"'}, {'“', '”'}
        };

        private readonly string _cmd;
        private int _ptr;
        private ISet<string> _flags = null; // Only parsed when requested first time

        private struct WordPosition
        {
            // Start of the word
            internal int startPos;
            
            // End of the word
            internal int endPos;
            
            // How much to advance word pointer afterwards to point at the start of the *next* word
            internal int advanceAfterWord;

            internal bool wasQuoted;

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
            _cmd = cmd.Replace("\n", " \n");
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
                if (_cmd[wp.startPos] != '-' || wp.wasQuoted) continue; // (if not, carry on w/ next word)
                
                // Find the *end* of the flag start (technically allowing arbitrary amounts of dashes)
                var flagNameStart = wp.startPos;
                while (flagNameStart < _cmd.Length && _cmd[flagNameStart] == '-')
                    flagNameStart++;

                // Then add the word to the flag set
                var word = _cmd.Substring(flagNameStart, wp.endPos - flagNameStart).Trim();
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
                if (_cmd[pos.startPos] == '-' && !pos.wasQuoted) continue;
                return _cmd.Substring(pos.startPos, pos.endPos - pos.startPos).Trim();
            }

            return "";
        }

        public string Peek()
        {
            // Loop to ignore and skip past flags, temp ptr so we don't move the real ptr
            var ptr = _ptr;
            while (NextWordPosition(ptr) is { } pos)
            {
                ptr = pos.endPos + pos.advanceAfterWord;
                if (_cmd[pos.startPos] == '-' && !pos.wasQuoted) continue;
                return _cmd.Substring(pos.startPos, pos.endPos - pos.startPos).Trim();
            }

            return "";
        }

        public ISet<string> Flags()
        {
            if (_flags == null) ParseFlags();
            return _flags;
        }

        public string Remainder()
        {
            // Skip all *leading* flags when taking the remainder
            while (NextWordPosition(_ptr) is {} wp)
            {
                if (_cmd[wp.startPos] != '-' || wp.wasQuoted) break;
                _ptr = wp.endPos + wp.advanceAfterWord;
            }
            
            // *Then* get the remainder
            return _cmd.Substring(Math.Min(_ptr, _cmd.Length)).Trim();
        }
        
        public string FullCommand => _cmd;

        private WordPosition? NextWordPosition(int position)
        {
            // Is this the end of the string?
            if (_cmd.Length <= position) return null;

            // Is this a quoted word?
            if (_quotePairs.ContainsKey(_cmd[position]))
            {
                // This is a quoted word, find corresponding end quote and return span
                var endQuote = _quotePairs[_cmd[position]];
                var endQuotePosition = _cmd.IndexOf(endQuote, position + 1);

                // Position after the end quote should be a space (or EOL)
                // Otherwise treat it as a standard word that's not quoted
                if (_cmd.Length == endQuotePosition + 1 || _cmd[endQuotePosition + 1] == ' ')
                {
                    if (endQuotePosition == -1)
                    {
                        // This is an unterminated quoted word, just return the entire word including the start quote
                        // TODO: should we do something else here?
                        return new WordPosition(position, _cmd.Length, 0, false);
                    }

                    return new WordPosition(position + 1, endQuotePosition, 2, true);
                }
            }

            // Not a quoted word, just find the next space and return if it's the end of the command
            var wordEnd = _cmd.IndexOf(' ', position + 1);
            
            return wordEnd == -1
                ? new WordPosition(position, _cmd.Length, 0, false)
                : new WordPosition(position, wordEnd, 1, false);
        }
    }
}