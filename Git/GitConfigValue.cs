using System;
using System.Collections.Generic;

namespace Git {

    public struct GitConfigValue : IEquatable<GitConfigValue> {
        public static implicit operator string(GitConfigValue configValue) => configValue.Value;

        private GitConfigFile m_configFile;
        private KeyValuePair<string, string> m_keyValue;

        internal GitConfigValue(GitConfigFile configFile, string key, string value) {
            m_configFile = configFile;
            m_keyValue = new KeyValuePair<string, string>(key, value);
        }

        public GitConfigFile ConfigFile => m_configFile;
        public KeyValuePair<string, string> Pair => m_keyValue;
        public string Key => Pair.Key;
        public string Value => Pair.Value;

        public T ToEnum<T>() => (T)Enum.Parse(typeof(T), Value, ignoreCase: true);

        public override bool Equals(object obj) => 
            obj is GitConfigValue ? Equals((GitConfigValue)obj) : false;
        public bool Equals(GitConfigValue other) => 
            string.Equals(Key, other.Key, StringComparison.CurrentCultureIgnoreCase) && 
            Value == other.Value;
        public override int GetHashCode() => Key.GetHashCode() ^ Value.GetHashCode();
        public override string ToString() => $"{Key}={Value}";
    }
}