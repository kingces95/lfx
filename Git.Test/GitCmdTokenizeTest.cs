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

            var token = GitCmdToken.Tokenize("a").Single();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual("a", token.Value);
            Assert.AreEqual(0, token.Position);

            Assert.IsTrue(token.ToString().Contains("a"));
            Assert.IsTrue(token.ToString().Contains("0"));
            Assert.IsTrue(token.ToString().Contains($"{GitCmdTokenType.Literal}"));
        }

        [Test]
        public static void AllTokensTest() {

            var tokens = GitCmdToken.Tokenize("a - -- \"b\"");
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

            var token = GitCmdToken.Tokenize("\"a b c\"").Single();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual("a b c", token.Value);
            Assert.AreEqual(0, token.Position);
        }

        [Test]
        public static void ConcatQuotedLiteralTest() {

            var token = GitCmdToken.Tokenize("ab\"cd\"ef\"gh\"ij").Single();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual("abcdefghij", token.Value);
            Assert.AreEqual(0, token.Position);
        }

        [Test]
        public static void QuotedQuoteTest() {

            var bs = "\\";
            var qu = "\"";
            var token = GitCmdToken.Tokenize($"{qu}{bs}{qu}{qu}").Single();
            Assert.AreEqual(GitCmdTokenType.Literal, token.Type);
            Assert.AreEqual($"{qu}", token.Value);
            Assert.AreEqual(0, token.Position);
        }

        [Test]
        public static void ThrowsTest() {

            Throws(() => GitCmdToken.Tokenize("\""));
            Throws(() => GitCmdToken.Tokenize("\\f"));
        }
    }
}
