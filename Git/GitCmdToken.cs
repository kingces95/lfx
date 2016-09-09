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
        WhiteSpace,
        Literal,
        Backslash,
        Quote,
        Dash,
        DoubleDash
    }

    public struct GitCmdToken {
        public static readonly Dictionary<GitCmdTokenType, string> TokenPatterns = 
            new Dictionary<GitCmdTokenType, string> {
                [GitCmdTokenType.WhiteSpace] = @"\s+",
                [GitCmdTokenType.Literal] = @"[^\s\\""-][^\s\\""]*",
                [GitCmdTokenType.Backslash] = @"\\",
                [GitCmdTokenType.Quote] = @"""",
                [GitCmdTokenType.Dash] = @"-(?!-)",
                [GitCmdTokenType.DoubleDash] = @"--",
            };
        public static readonly string TokenPattern =
            $"^({string.Join("|", TokenPatterns.Select(o => $"(?<{o.Key}>{o.Value})"))})*$";

        public static CmdTokens Tokenize(string commandLine) {
            return new CmdTokens(GenerateTokens(commandLine));
        }
        private static IEnumerable<GitCmdToken> GenerateTokens(string commandLine) {
            var match = Regex.Match(commandLine, TokenPattern);

            if (!match.Success)
                throw new GitCmdException(
                    $"Failed to parse command line '{commandLine}' into tokens using pattern: {TokenPattern}");

            var tokens = new CmdTokens(
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

                if (type == GitCmdTokenType.Backslash)
                    throw new GitCmdException($"Unexpected escape token '{token}'");

                else if (type == GitCmdTokenType.Quote || type == GitCmdTokenType.Literal)
                    yield return ParseLiteral(tokens);

                else if (token.Type == GitCmdTokenType.WhiteSpace)
                    continue;

                else
                    yield return token;
            }
        }

        private static GitCmdToken ParseLiteral(CmdTokens tokens) {
            var sb = new StringBuilder();
            var position = tokens.Current.Position;

            var token = tokens.Current;
            while (true) {
                if (token.Type == GitCmdTokenType.Quote)
                    ParseQuotedString(tokens, sb);

                else if (token.Type == GitCmdTokenType.Literal)
                    sb.Append(token.Value);

                else 
                    return new GitCmdToken(GitCmdTokenType.Literal, sb.ToString(), position);

                token = tokens.DequeueOrDefault();
            }
        }

        private static void ParseQuotedString(CmdTokens tokens, StringBuilder sb) {

            while (true) {
                var token = tokens.Dequeue();

                if (token.Type == GitCmdTokenType.Quote)
                    return;

                if (token.Type == GitCmdTokenType.Backslash && tokens.Peek().Type == GitCmdTokenType.Quote)
                    token = tokens.Dequeue();

                sb.Append(token.Value);
            }
        }

        public GitCmdToken(GitCmdTokenType type, string value, int position) {
            Type = type;
            Value = value;
            Position = position;
        }
        public GitCmdTokenType Type;
        public string Value;
        public int Position;

        public override string ToString() => $"{Type}, position={Position}, value={Value}";
    }

    public struct CmdTokens : IEnumerable<GitCmdToken> {
        private Queue<GitCmdToken> m_queue;
        private GitCmdToken m_current;

        internal CmdTokens(IEnumerable<GitCmdToken> tokens) {
            m_queue = new Queue<GitCmdToken>(tokens);
            m_current = default(GitCmdToken);
        }

        public GitCmdToken Current => m_current;
        public bool Any() => m_queue.Any();
        public GitCmdToken Dequeue(GitCmdTokenType type) {
            var token = Dequeue().Type;
            if ((type != token))
                throw new GitCmdException($"Expected token '{m_current}' to be of type '{type}'.");

            return m_current;
        }
        public GitCmdToken DequeueOrDefault() => Any() ? Dequeue() : default(GitCmdToken);
        public GitCmdToken Dequeue() {
            if (!m_queue.Any())
                throw new GitCmdException($"Unexpected end of command line encountered.");

            return m_current = m_queue.Dequeue();
        }
        public GitCmdToken Peek() => m_queue.Any() ? m_queue.Peek() : default(GitCmdToken);
        public void ParseError() {
            throw new GitCmdException($"Unexpected token '{m_current}' found in command line.");
        }

        public IEnumerator<GitCmdToken> GetEnumerator() => m_queue.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}