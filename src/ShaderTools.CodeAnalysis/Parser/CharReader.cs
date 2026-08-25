using System;
using Microsoft.CodeAnalysis.Text;

namespace ShaderTools.CodeAnalysis.Parser
{
    internal sealed class CharReader
    {
        private readonly SourceText _text;

        public CharReader(SourceText text)
        {
            _text = text;
        }

        // Position is clamped to [0, text.Length]. Tokens are created from spans ending at
        // Position, so allowing it past the end of the text would make SourceText.GetSubText
        // throw for input that is truncated mid-token (e.g. a file ending in "\).
        public void NextChar()
        {
            if (Position < _text.Length)
                Position++;
        }

        public int Position { get; private set; }

        public char Current => Peek(0);

        public char Peek() => Peek(1);

        public char Peek(int offset)
        {
            var index = Position + offset;
            return (index < _text.Length)
                ? _text[index]
                : '\0';
        }

        public void Reset(int position)
        {
            Position = Math.Max(0, Math.Min(position, _text.Length));
        }
    }
}