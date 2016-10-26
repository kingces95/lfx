using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Git {

    public sealed class GitCmdException : Exception {
        public GitCmdException(string message) : base(message) { }
    }

    public enum GitCmdTokenType {
        Unknown,
        EndOfStram,
        WhiteSpace,
        Literal,
        Backslash,
        Quote,
        Dash,
        DoubleDash
    }

    public struct GitCmdToken {
        public GitCmdToken(GitCmdTokenType type, string value, int position) {
            Type = type;
            Value = value ?? string.Empty;
            Position = position;
        }
        public GitCmdTokenType Type;
        public string Value;
        public int Position;

        public override string ToString() => $"{Type}, position={Position}, value={Value}";
    }

    public class GitCmdTokens : IEnumerable<GitCmdToken> {
        public static readonly Dictionary<GitCmdTokenType, string> TokenPatterns = 
            new Dictionary<GitCmdTokenType, string> {
                [GitCmdTokenType.WhiteSpace] = @"\s+",
                [GitCmdTokenType.Literal] = @"[^\s\\""-][^\s\\""]*",
                [GitCmdTokenType.Backslash] = @"\\",
                [GitCmdTokenType.Quote] = @"""",
                [GitCmdTokenType.Dash] = @"-(?!-)",
                [GitCmdTokenType.DoubleDash] = @"--",
                [GitCmdTokenType.EndOfStram] = @"$",
            };
        public static readonly string TokenPattern =
            $"^({string.Join("|", TokenPatterns.Select(o => $"(?<{o.Key}>{o.Value})"))})*";

        public static GitCmdTokens Tokenize(string commandLine) {
            return new GitCmdTokens(GenerateTokens(commandLine));
        }
        private static IEnumerable<GitCmdToken> GenerateTokens(string commandLine) {
            var match = Regex.Match(commandLine, TokenPattern);

            if (!match.Success)
                throw new GitCmdException(
                    $"Failed to parse command line '{commandLine}' into tokens using pattern: {TokenPattern}");

            var tokens = new GitCmdTokens(
                from pair in TokenPatterns
                let token = pair.Key
                from Capture capture in match.Groups[$"{token}"].Captures
                orderby capture.Index
                select new GitCmdToken(token, capture.Value, capture.Index)
            );

            // unescape quoted string into literal
            while (tokens.Any()) {
                var token = tokens.Dequeue();
                var type = token.Type;

                if (type == GitCmdTokenType.WhiteSpace)
                    continue;

                if (type == GitCmdTokenType.Quote || 
                    type == GitCmdTokenType.Backslash ||
                    type == GitCmdTokenType.Literal)
                    yield return ParseLiteral(tokens);

                else
                    yield return token;
            }
        }

        private static GitCmdToken ParseLiteral(GitCmdTokens tokens) {
            var sb = new StringBuilder();
            var position = tokens.Current.Position;

            var token = tokens.Current;
            while (true) {
                if (token.Type == GitCmdTokenType.Quote)
                    ParseQuotedString(tokens, sb);

                else if (token.Type == GitCmdTokenType.Literal)
                    sb.Append(token.Value);

                else if (token.Type == GitCmdTokenType.Backslash)
                    sb.Append(token.Value);

                else {
                    tokens.Requeue();
                    return new GitCmdToken(GitCmdTokenType.Literal, sb.ToString(), position);
                }

                token = tokens.Dequeue();
            }
        }

        private static void ParseQuotedString(GitCmdTokens tokens, StringBuilder sb) {

            while (true) {
                var token = tokens.Dequeue();

                if (token.Type == GitCmdTokenType.Quote)
                    return;

                if (token.Type == GitCmdTokenType.Backslash && tokens.Peek().Type == GitCmdTokenType.Quote)
                    token = tokens.Dequeue();

                sb.Append(token.Value);
            }
        }

        private GitCmdToken[] m_tokens;
        private int m_index;
        private GitCmdToken m_current;

        internal GitCmdTokens(IEnumerable<GitCmdToken> tokens) {
            m_tokens = tokens.ToArray();
            m_index = -1;
        }

        public GitCmdToken Current => m_current;
        public bool Any() => m_index + 1 != m_tokens.Length;
        public void Requeue() {
            if (m_current.Type == GitCmdTokenType.Unknown)
                throw new InvalidOperationException();

            m_index--;
            m_current = default(GitCmdToken);
        }
        public GitCmdToken Dequeue(params GitCmdTokenType[] expectedType) {
            var type = Dequeue().Type;
            if (expectedType != null && !expectedType.Contains(type))
                throw new GitCmdException(
                    $"Expected token '{m_current}' to be of type '{string.Join(", or ", expectedType)}'.");

            return m_current;
        }
        public GitCmdToken Dequeue() {
            if (!Any())
                throw new GitCmdException($"Unexpected end of command line encountered.");

            return m_current = m_tokens[++m_index];
        }
        public GitCmdToken Peek() => Any() ? m_tokens[m_index + 1] : default(GitCmdToken);
        public void ParseError() {
            throw new GitCmdException($"Unexpected token '{m_current}' found in command line.");
        }

        public IEnumerator<GitCmdToken> GetEnumerator() {
            if (!Any())
                return Enumerable.Empty<GitCmdToken>().GetEnumerator();

            return m_tokens.Skip(m_index + 1).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}