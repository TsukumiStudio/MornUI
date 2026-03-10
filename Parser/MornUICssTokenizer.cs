using System.Collections.Generic;
using System.Text;

namespace MornLib
{
    internal enum MornUICssTokenType
    {
        Ident,
        Hash,
        Number,
        Colon,
        Semicolon,
        LBrace,
        RBrace,
        Dot,
        Comma,
        Whitespace,
        String,
        AtKeyword,
        Gt,
        Eof,
    }

    internal readonly struct MornUICssToken
    {
        public readonly MornUICssTokenType Type;
        public readonly string Value;

        public MornUICssToken(MornUICssTokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        public override string ToString() => $"{Type}: \"{Value}\"";
    }

    internal sealed class MornUICssTokenizer
    {
        private readonly string _source;
        private int _pos;

        public MornUICssTokenizer(string source)
        {
            _source = source ?? string.Empty;
            _pos = 0;
        }

        public List<MornUICssToken> Tokenize()
        {
            var tokens = new List<MornUICssToken>();
            while (_pos < _source.Length)
            {
                var c = _source[_pos];
                switch (c)
                {
                    case '/' when Peek(1) == '*':
                        SkipBlockComment();
                        break;
                    case '{':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.LBrace, "{"));
                        _pos++;
                        break;
                    case '}':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.RBrace, "}"));
                        _pos++;
                        break;
                    case ':':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.Colon, ":"));
                        _pos++;
                        break;
                    case ';':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.Semicolon, ";"));
                        _pos++;
                        break;
                    case '.':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.Dot, "."));
                        _pos++;
                        break;
                    case ',':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.Comma, ","));
                        _pos++;
                        break;
                    case '>':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.Gt, ">"));
                        _pos++;
                        break;
                    case '(':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.Ident, "("));
                        _pos++;
                        break;
                    case ')':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.Ident, ")"));
                        _pos++;
                        break;
                    case '#':
                        _pos++;
                        tokens.Add(new MornUICssToken(MornUICssTokenType.Hash, ReadIdentOrHex()));
                        break;
                    case '@':
                        _pos++;
                        tokens.Add(new MornUICssToken(MornUICssTokenType.AtKeyword, ReadIdent()));
                        break;
                    case '"' or '\'':
                        tokens.Add(new MornUICssToken(MornUICssTokenType.String, ReadString(c)));
                        break;
                    default:
                        if (char.IsWhiteSpace(c))
                        {
                            SkipWhitespace();
                            tokens.Add(new MornUICssToken(MornUICssTokenType.Whitespace, " "));
                        }
                        else if (IsIdentStart(c) || c == '-')
                        {
                            tokens.Add(new MornUICssToken(MornUICssTokenType.Ident, ReadIdent()));
                        }
                        else if (char.IsDigit(c))
                        {
                            tokens.Add(new MornUICssToken(MornUICssTokenType.Number, ReadNumber()));
                        }
                        else
                        {
                            _pos++;
                        }

                        break;
                }
            }

            tokens.Add(new MornUICssToken(MornUICssTokenType.Eof, ""));
            return tokens;
        }

        private char Peek(int offset)
        {
            var idx = _pos + offset;
            return idx < _source.Length ? _source[idx] : '\0';
        }

        private void SkipBlockComment()
        {
            _pos += 2;
            while (_pos < _source.Length - 1)
            {
                if (_source[_pos] == '*' && _source[_pos + 1] == '/')
                {
                    _pos += 2;
                    return;
                }

                _pos++;
            }

            _pos = _source.Length;
        }

        private void SkipWhitespace()
        {
            while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos]))
                _pos++;
        }

        private string ReadIdent()
        {
            var sb = new StringBuilder();
            while (_pos < _source.Length && IsIdentChar(_source[_pos]))
            {
                sb.Append(_source[_pos]);
                _pos++;
            }

            return sb.ToString();
        }

        private string ReadIdentOrHex()
        {
            var sb = new StringBuilder();
            while (_pos < _source.Length && (IsIdentChar(_source[_pos]) || char.IsDigit(_source[_pos])))
            {
                sb.Append(_source[_pos]);
                _pos++;
            }

            return sb.ToString();
        }

        private string ReadNumber()
        {
            var sb = new StringBuilder();
            var hasDot = false;
            if (_pos < _source.Length && _source[_pos] == '-')
            {
                sb.Append('-');
                _pos++;
            }

            while (_pos < _source.Length)
            {
                var c = _source[_pos];
                if (char.IsDigit(c))
                {
                    sb.Append(c);
                    _pos++;
                }
                else if (c == '.' && !hasDot)
                {
                    hasDot = true;
                    sb.Append(c);
                    _pos++;
                }
                else
                {
                    break;
                }
            }

            return sb.ToString();
        }

        private string ReadString(char quote)
        {
            _pos++;
            var sb = new StringBuilder();
            while (_pos < _source.Length && _source[_pos] != quote)
            {
                if (_source[_pos] == '\\' && _pos + 1 < _source.Length)
                {
                    _pos++;
                    sb.Append(_source[_pos]);
                }
                else
                {
                    sb.Append(_source[_pos]);
                }

                _pos++;
            }

            if (_pos < _source.Length) _pos++;
            return sb.ToString();
        }

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';
    }
}
