using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace Git {

    public sealed class GitCmdSwitchInfo {
        public static IEnumerable<GitCmdSwitchInfo> Create(params string[] names) {
            foreach (var name in names)
                yield return new GitCmdSwitchInfo(name);
        }
        public static IEnumerable<GitCmdSwitchInfo> Create<T>(params T[] names) where T : struct {
            foreach (var name in names)
                yield return new GitCmdSwitchInfo(name.ToString(), o => TryParseEnum<T>(o));
        }
        public static GitCmdSwitchInfo Blank = 
            new GitCmdSwitchInfo(string.Empty, o => string.IsNullOrEmpty(o) ? string.Empty : null);

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

        public GitCmdSwitchInfo(string name, Func<string, object> tryParse = null, bool hasValue = false) {
            m_name = name;
            m_hasValue = hasValue;
            m_tryParse = tryParse;
        }

        public string Name => m_name;
        public bool HasValue => m_hasValue;
        public object TryParse(string value) => m_tryParse?.Invoke(value) ?? value;

        public override string ToString() => $"{Name}, hasValue={HasValue}";
    }

    public sealed class GitCmdArgs : IEnumerable<string> {
        public static GitCmdArgs Parse(
            string commandLine, 
            int minArgs = 0,
            int maxArgs = int.MaxValue,
            IEnumerable<GitCmdSwitchInfo> switchInfo = null) => 
                new GitCmdArgs(commandLine, minArgs, maxArgs, switchInfo);

        private Dictionary<string, GitCmdSwitchInfo> m_switchInfo;
        private HashSet<object> m_switches;
        private List<string> m_arguments;
        private string m_exe;
        private string m_name;

        private GitCmdArgs(
            string args, 
            int minArgs,
            int maxArgs,
            IEnumerable<GitCmdSwitchInfo> switchInfos = null) {

            if (switchInfos != null)
                m_switchInfo = switchInfos
                    .ToDictionary(o => o.Name, StringComparer.InvariantCultureIgnoreCase);
            m_switches = new HashSet<object>();
            m_arguments = new List<string>();

            var tokens = GitCmdTokens.Tokenize(args);
            Parse(tokens);

            if (Length < minArgs)
                throw new GitCmdException($"Expected at least '{minArgs}' arguments.");

            if (Length > maxArgs)
                throw new GitCmdException($"Expected at most '{maxArgs}' arguments.");
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
        private void Parse(GitCmdTokens tokens) {
            m_exe = tokens.Dequeue(GitCmdTokenType.Literal).Value;

            var token = tokens.Dequeue();
            if (token.Type == GitCmdTokenType.EndOfStram)
                return;

            if (token.Type != GitCmdTokenType.Literal)
                tokens.ParseError();
            m_name = token.Value;

            while (tokens.Any()) {
                token = tokens.Dequeue();

                if (token.Type == GitCmdTokenType.Dash)
                    ParseShortSwitch(tokens);

                else if (token.Type == GitCmdTokenType.DoubleDash)
                    ParseLongSwitch(tokens);

                else if (token.Type == GitCmdTokenType.Literal)
                    m_arguments.Add(token.Value);

                else if (token.Type == GitCmdTokenType.EndOfStram)
                    break;

                else
                    tokens.ParseError();
            }
        }
        private void ParseShortSwitch(GitCmdTokens tokens) {
            var token = tokens.Dequeue();
            if (token.Type != GitCmdTokenType.Literal)
                tokens.ParseError();

            if (token.Value.Length != 1)
                throw new GitCmdException(
                    $"Token '{token}' following single dash must be a single character literal.");

            AddSwitch(token);
        }
        private void ParseLongSwitch(GitCmdTokens tokens) {
            var token = tokens.Dequeue(
                GitCmdTokenType.Literal, 
                GitCmdTokenType.WhiteSpace,
                GitCmdTokenType.EndOfStram
            );

            if (token.Value.Length == 1)
                throw new GitCmdException(
                    $"Token '{token}' following single dash must be multi-character literal.");

            AddSwitch(token);
        }
        private void AddSwitch(GitCmdToken token) {

            var value = token.Value.Trim();
            var swtch = ParseSwitch(value);

            if (swtch == null)
                throw new GitCmdException($"Switch '{value}' is not supported for this command.");

            if (IsSet(swtch))
                throw new GitCmdException($"Switch '{token.Value}' must be set only once.");

            m_switches.Add(swtch);
        }

        public GitCmdSwitchInfo GetSwitchInfo(string name) {
            GitCmdSwitchInfo info;
            m_switchInfo.TryGetValue(name, out info);
            return info;
        }
        public IEnumerable<GitCmdSwitchInfo> SwitchInfos() => m_switchInfo.Values;
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
}