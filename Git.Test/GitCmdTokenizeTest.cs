using NUnit.Framework;
using System.Linq;
using System;
using System.IO;

namespace Git.Test {

    [TestFixture]
    public class GitCmdTokenizeTest
    {

        private static void Throws(TestDelegate action) {
            Assert.Throws<GitCmdException>(action);
        }

        [Test]
        public static void SimpleLiteralTest() {

            var tokens = GitCmdTokens.Tokenize("a");
            Assert.AreEqual(2, tokens.Count());

            var token = tokens.First();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual("a", token.Value);
            Assert.AreEqual(0, token.Position);

            Assert.IsTrue(token.ToString().Contains("a"));
            Assert.IsTrue(token.ToString().Contains("0"));
            Assert.IsTrue(token.ToString().Contains($"{GitCmdTokenType.Literal}"));
        }

        [Test]
        public static void AllTokensTest() {

            var tokens = GitCmdTokens.Tokenize("a - -- \"b\"");
            GitCmdToken token;
            
            token = tokens.Dequeue();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual("a", token.Value);
            Assert.AreEqual(0, token.Position);

            token = tokens.Dequeue();
            Assert.AreEqual(GitCmdTokenType.Dash, token.Type);
            Assert.AreEqual("-", token.Value);
            Assert.AreEqual(2, token.Position);

            token = tokens.Dequeue();
            Assert.AreEqual(GitCmdTokenType.DoubleDash, token.Type);
            Assert.AreEqual("--", token.Value);
            Assert.AreEqual(4, token.Position);

            token = tokens.Dequeue();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual("b", token.Value);
            Assert.AreEqual(7, token.Position);
        }

        [Test]
        public static void QuotedLiteralTest() {

            var tokens = GitCmdTokens.Tokenize("\"a b c\"");
            Assert.AreEqual(2, tokens.Count());

            var token = GitCmdTokens.Tokenize("\"a b c\"").First();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual("a b c", token.Value);
            Assert.AreEqual(0, token.Position);
        }

        [Test]
        public static void ConcatQuotedLiteralTest() {

            var tokens = GitCmdTokens.Tokenize("ab\"cd\"ef\"gh\"ij");
            Assert.AreEqual(2, tokens.Count());

            var token = GitCmdTokens.Tokenize("ab\"cd\"ef\"gh\"ij").First();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual("abcdefghij", token.Value);
            Assert.AreEqual(0, token.Position);
        }

        [Test]
        public static void QuotedQuoteTest() {

            var bs = "\\";
            var qu = "\"";
            var tokens = GitCmdTokens.Tokenize($"{qu}{bs}{qu}{qu}");
            Assert.AreEqual(2, tokens.Count());

            var token = tokens.First();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual($"{qu}", token.Value);
            Assert.AreEqual(0, token.Position);
        }

        [Test]
        public static void ThrowsTest() {

            Throws(() => GitCmdTokens.Tokenize("\""));
        }
    }
}
