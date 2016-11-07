using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;

namespace Lfx {

    public sealed class LfxCmdSwitchInfo {
        public static IEnumerable<LfxCmdSwitchInfo> Create(params string[] names) {
            foreach (var name in names)
                yield return new LfxCmdSwitchInfo(name);
        }
        public static IEnumerable<LfxCmdSwitchInfo> Create<T>(params T[] names) where T : struct {
            foreach (var name in names)
                yield return new LfxCmdSwitchInfo(name.ToString(), o => TryParseEnum<T>(o));
        }
        public static LfxCmdSwitchInfo Blank = 
            new LfxCmdSwitchInfo(string.Empty, o => string.IsNullOrEmpty(o) ? string.Empty : null);

        private static object TryParseEnum<T>(string value) where T : struct {
            var ignoreCase = true;

            T result;
            if (!Enum.TryParse(value, ignoreCase, out result))
                return null;
            return result;
        }

        private readonly string m_name;
        private readonly bool m_hasValue;
        private readonly Func<string, object> m_tryParse;

        public LfxCmdSwitchInfo(string name, Func<string, object> tryParse = null, bool hasValue = false) {
            m_name = name;
            m_hasValue = hasValue;
            m_tryParse = tryParse;
        }

        public string Name => m_name;
        public bool HasValue => m_hasValue;
        public object TryParse(string value) => m_tryParse?.Invoke(value) ?? value;

        public override string ToString() => $"{Name}, hasValue={HasValue}";
    }

    public sealed class LfxCmdArgs : IEnumerable<string> {
        public static LfxCmdArgs Parse(
            string commandLine, 
            int minArgs = 0,
            int maxArgs = int.MaxValue,
            IEnumerable<LfxCmdSwitchInfo> switchInfo = null) => 
                new LfxCmdArgs(commandLine, minArgs, maxArgs, switchInfo);

        private Dictionary<string, LfxCmdSwitchInfo> m_switchInfo;
        private HashSet<object> m_switches;
        private List<string> m_arguments;
        private string m_exe;
        private string m_name;

        private LfxCmdArgs(
            string args, 
            int minArgs,
            int maxArgs,
            IEnumerable<LfxCmdSwitchInfo> switchInfos = null) {

            if (switchInfos != null)
                m_switchInfo = switchInfos
                    .ToDictionary(o => o.Name, StringComparer.InvariantCultureIgnoreCase);
            m_switches = new HashSet<object>();
            m_arguments = new List<string>();

            var tokens = LfxCmdTokens.Tokenize(args);
            Parse(tokens);

            if (Length < minArgs)
                throw new LfxCmdException($"Expected at least '{minArgs}' arguments.");

            if (Length > maxArgs)
                throw new LfxCmdException($"Expected at most '{maxArgs}' arguments.");
        }

        private object ToLowerIfString(object value) {
            var resultString = value as string;
            if (resultString != null)
                value = resultString.ToLower();
            return value;
        }
        private object ParseSwitch(string value) {
            object result = value;
            if (m_switchInfo != null) {
                var info = GetSwitchInfo(value);
                if (info == null)
                    return null;

                result = info.TryParse(value);
            }

            return ToLowerIfString(result);
        }
        private void Parse(LfxCmdTokens tokens) {
            m_exe = tokens.Dequeue(LfxCmdTokenType.Literal).Value;

            var token = tokens.Dequeue();
            if (token.Type == LfxCmdTokenType.EndOfStram)
                return;

            if (token.Type != LfxCmdTokenType.Literal)
                tokens.ParseError();
            m_name = token.Value;

            while (tokens.Any()) {
                token = tokens.Dequeue();

                if (token.Type == LfxCmdTokenType.Dash)
                    ParseShortSwitch(tokens);

                else if (token.Type == LfxCmdTokenType.DoubleDash)
                    ParseLongSwitch(tokens);

                else if (token.Type == LfxCmdTokenType.Literal)
                    m_arguments.Add(token.Value);

                else if (token.Type == LfxCmdTokenType.EndOfStram)
                    break;

                else
                    tokens.ParseError();
            }
        }
        private void ParseShortSwitch(LfxCmdTokens tokens) {
            var token = tokens.Dequeue();
            if (token.Type != LfxCmdTokenType.Literal)
                tokens.ParseError();

            if (token.Value.Length != 1)
                throw new LfxCmdException(
                    $"Token '{token}' following single dash must be a single character literal.");

            AddSwitch(token);
        }
        private void ParseLongSwitch(LfxCmdTokens tokens) {
            var token = tokens.Dequeue(
                LfxCmdTokenType.Literal, 
                LfxCmdTokenType.WhiteSpace,
                LfxCmdTokenType.EndOfStram
            );

            if (token.Value.Length == 1)
                throw new LfxCmdException(
                    $"Token '{token}' following single dash must be multi-character literal.");

            AddSwitch(token);
        }
        private void AddSwitch(LfxCmdToken token) {

            var value = token.Value.Trim();
            var swtch = ParseSwitch(value);

            if (swtch == null)
                throw new LfxCmdException($"Switch '{value}' is not supported for this command.");

            if (IsSet(swtch))
                throw new LfxCmdException($"Switch '{token.Value}' must be set only once.");

            m_switches.Add(swtch);
        }

        public LfxCmdSwitchInfo GetSwitchInfo(string name) {
            LfxCmdSwitchInfo info;
            m_switchInfo.TryGetValue(name, out info);
            return info;
        }
        public IEnumerable<LfxCmdSwitchInfo> SwitchInfos() => m_switchInfo.Values;
        public IEnumerable<object> Switches() => m_switches;
        public bool IsSet(params object[] swtch) => swtch.Any(o => m_switches.Contains(ToLowerIfString(o)));

        public string Exe => m_exe;
        public string Name => m_name;
        public IReadOnlyList<string> Arguments => m_arguments;
        public int Length => Arguments.Count;
        public string this[int position] => Arguments[position];
        public IEnumerator<string> GetEnumerator() => Arguments.Cast<string>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => 
            $"{Exe} {Name}, switches: {string.Join(", ", m_switches)}, args: {string.Join(" ", m_arguments)}";
    }

    public sealed class LfxCmdException : Exception {
        public LfxCmdException(string message) : base(message) { }
    }

    public enum LfxCmdTokenType {
        Unknown,
        EndOfStram,
        WhiteSpace,
        Literal,
        Backslash,
        Quote,
        Dash,
        DoubleDash
    }

    public struct LfxCmdToken {
        public LfxCmdToken(LfxCmdTokenType type, string value, int position) {
            Type = type;
            Value = value ?? string.Empty;
            Position = position;
        }
        public LfxCmdTokenType Type;
        public string Value;
        public int Position;

        public override string ToString() => $"{Type}, position={Position}, value={Value}";
    }

    public class LfxCmdTokens : IEnumerable<LfxCmdToken> {
        public static readonly Dictionary<LfxCmdTokenType, string> TokenPatterns =
            new Dictionary<LfxCmdTokenType, string> {
                [LfxCmdTokenType.WhiteSpace] = @"\s+",
                [LfxCmdTokenType.Literal] = @"[^\s\\""-][^\s\\""]*",
                [LfxCmdTokenType.Backslash] = @"\\",
                [LfxCmdTokenType.Quote] = @"""",
                [LfxCmdTokenType.Dash] = @"-(?!-)",
                [LfxCmdTokenType.DoubleDash] = @"--",
                [LfxCmdTokenType.EndOfStram] = @"$",
            };
        public static readonly string TokenPattern =
            $"^({string.Join("|", TokenPatterns.Select(o => $"(?<{o.Key}>{o.Value})"))})*";

        public static LfxCmdTokens Tokenize(string commandLine) {
            return new LfxCmdTokens(GenerateTokens(commandLine));
        }
        private static IEnumerable<LfxCmdToken> GenerateTokens(string commandLine) {
            var match = Regex.Match(commandLine, TokenPattern);

            if (!match.Success)
                throw new LfxCmdException(
                    $"Failed to parse command line '{commandLine}' into tokens using pattern: {TokenPattern}");

            var tokens = new LfxCmdTokens(
                from pair in TokenPatterns
                let token = pair.Key
                from Capture capture in match.Groups[$"{token}"].Captures
                orderby capture.Index
                select new LfxCmdToken(token, capture.Value, capture.Index)
            );

            // unescape quoted string into literal
            while (tokens.Any()) {
                var token = tokens.Dequeue();
                var type = token.Type;

                if (type == LfxCmdTokenType.WhiteSpace)
                    continue;

                if (type == LfxCmdTokenType.Quote ||
                    type == LfxCmdTokenType.Backslash ||
                    type == LfxCmdTokenType.Literal)
                    yield return ParseLiteral(tokens);

                else
                    yield return token;
            }
        }

        private static LfxCmdToken ParseLiteral(LfxCmdTokens tokens) {
            var sb = new StringBuilder();
            var position = tokens.Current.Position;

            var token = tokens.Current;
            while (true) {
                if (token.Type == LfxCmdTokenType.Quote)
                    ParseQuotedString(tokens, sb);

                else if (token.Type == LfxCmdTokenType.Literal)
                    sb.Append(token.Value);

                else if (token.Type == LfxCmdTokenType.Backslash)
                    sb.Append(token.Value);

                else {
                    tokens.Requeue();
                    return new LfxCmdToken(LfxCmdTokenType.Literal, sb.ToString(), position);
                }

                token = tokens.Dequeue();
            }
        }

        private static void ParseQuotedString(LfxCmdTokens tokens, StringBuilder sb) {

            while (true) {
                var token = tokens.Dequeue();

                if (token.Type == LfxCmdTokenType.Quote)
                    return;

                if (token.Type == LfxCmdTokenType.Backslash && tokens.Peek().Type == LfxCmdTokenType.Quote)
                    token = tokens.Dequeue();

                sb.Append(token.Value);
            }
        }

        private LfxCmdToken[] m_tokens;
        private int m_index;
        private LfxCmdToken m_current;

        internal LfxCmdTokens(IEnumerable<LfxCmdToken> tokens) {
            m_tokens = tokens.ToArray();
            m_index = -1;
        }

        public LfxCmdToken Current => m_current;
        public bool Any() => m_index + 1 != m_tokens.Length;
        public void Requeue() {
            if (m_current.Type == LfxCmdTokenType.Unknown)
                throw new InvalidOperationException();

            m_index--;
            m_current = default(LfxCmdToken);
        }
        public LfxCmdToken Dequeue(params LfxCmdTokenType[] expectedType) {
            var type = Dequeue().Type;
            if (expectedType != null && !expectedType.Contains(type))
                throw new LfxCmdException(
                    $"Expected token '{m_current}' to be of type '{string.Join(", or ", expectedType)}'.");

            return m_current;
        }
        public LfxCmdToken Dequeue() {
            if (!Any())
                throw new LfxCmdException($"Unexpected end of command line encountered.");

            return m_current = m_tokens[++m_index];
        }
        public LfxCmdToken Peek() => Any() ? m_tokens[m_index + 1] : default(LfxCmdToken);
        public void ParseError() {
            throw new LfxCmdException($"Unexpected token '{m_current}' found in command line.");
        }

        public IEnumerator<LfxCmdToken> GetEnumerator() {
            if (!Any())
                return Enumerable.Empty<LfxCmdToken>().GetEnumerator();

            return m_tokens.Skip(m_index + 1).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}