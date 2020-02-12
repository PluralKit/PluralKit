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

        public Parameters(string cmd)
        {
            // This is a SUPER dirty hack to avoid having to match both spaces and newlines in the word detection below
            // Instead, we just add a space before every newline (which then gets stripped out later).
            _cmd = cmd.Replace("\n", " \n");
            _ptr = 0;
        }

        public string Pop()
        {
            var positions = NextWordPosition();
            if (positions == null) return "";

            var (start, end, advance) = positions.Value;
            _ptr = end + advance;
            return _cmd.Substring(start, end - start).Trim();
        }

        public string Peek()
        {
            var positions = NextWordPosition();
            if (positions == null) return "";

            var (start, end, _) = positions.Value;
            return _cmd.Substring(start, end - start).Trim();
        }

        public string Remainder() => _cmd.Substring(Math.Min(_ptr, _cmd.Length)).Trim();
        public string FullCommand => _cmd;

        // Returns tuple of (startpos, endpos, advanceafter)
        // advanceafter is how much to move the pointer afterwards to point it
        // at the start of the next word
        private ValueTuple<int, int, int>? NextWordPosition()
        {
            // Is this the end of the string?
            if (_cmd.Length <= _ptr) return null;

            // Is this a quoted word?
            if (_quotePairs.ContainsKey(_cmd[_ptr]))
            {
                // This is a quoted word, find corresponding end quote and return span
                var endQuote = _quotePairs[_cmd[_ptr]];
                var endQuotePosition = _cmd.IndexOf(endQuote, _ptr + 1);
                
                // Position after the end quote should be a space (or EOL)
                // Otherwise treat it as a standard word that's not quoted
                if (_cmd.Length == endQuotePosition + 1 || _cmd[endQuotePosition + 1] == ' ')
                {
                    if (endQuotePosition == -1)
                    {
                        // This is an unterminated quoted word, just return the entire word including the start quote
                        // TODO: should we do something else here?
                        return (_ptr, _cmd.Length, 0);
                    }

                    return (_ptr + 1, endQuotePosition, 2);
                }
            }

            // Not a quoted word, just find the next space and return as appropriate
            var wordEnd = _cmd.IndexOf(' ', _ptr + 1);
            return wordEnd != -1 ? (_ptr, wordEnd, 1) : (_ptr, _cmd.Length, 0);
        }
    }
}