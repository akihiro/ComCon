namespace ComCon.Tests
{
    using System;
    using System.Text;
    using Xunit;

    public class NewlineTranslatorTests
    {
        [Theory]
        [InlineData("a\nb\n", "a\nb\n")]
        [InlineData("a\rb\r\n", "a\rb\r\n")]
        [InlineData("\x1b[31mred\x1b[0m", "\x1b[31mred\x1b[0m")]
        public void NoneLeavesDataUnchanged(string input, string expected)
        {
            Assert.Equal(expected, Translate(ReceiveNewline.None, input));
        }

        [Theory]
        [InlineData("a\nb\n", "a\r\nb\r\n")]
        [InlineData("a\r\nb\r\n", "a\r\nb\r\n")]
        [InlineData("\n\n", "\r\n\r\n")]
        [InlineData("progress 10%\rprogress 20%\r", "progress 10%\rprogress 20%\r")]
        [InlineData("\x1b[2J\x1b[H$ ", "\x1b[2J\x1b[H$ ")]
        public void LfModeAddsCrBeforeBareLf(string input, string expected)
        {
            Assert.Equal(expected, Translate(ReceiveNewline.Lf, input));
        }

        [Theory]
        [InlineData("a\rb\r", "a\r\nb\r\n")]
        [InlineData("a\r\nb\r\n", "a\r\nb\r\n")]
        [InlineData("\r\r", "\r\n\r\n")]
        [InlineData("a\nb", "a\nb")]
        public void CrModeTurnsCrIntoCrLf(string input, string expected)
        {
            Assert.Equal(expected, Translate(ReceiveNewline.Cr, input));
        }

        [Theory]
        [InlineData("Lf")]
        [InlineData("Cr")]
        public void CrLfSplitAcrossReadsIsNotDoubled(string mode)
        {
            var m = (ReceiveNewline)Enum.Parse(typeof(ReceiveNewline), mode);
            Assert.Equal("line\r\nnext", Translate(m, "line\r", "\nnext"));
        }

        [Fact]
        public void LfModeBareLfAtStartOfSecondRead()
        {
            Assert.Equal("a\r\nb", Translate(ReceiveNewline.Lf, "a", "\nb"));
        }

        [Fact]
        public void CrModeLfAfterOtherCharactersIsKept()
        {
            // Only the LF directly after a CR is part of a CR LF pair.
            Assert.Equal("a\r\nb\n", Translate(ReceiveNewline.Cr, "a\r", "b\n"));
        }

        [Fact]
        public void WorstCaseFitsInMaxOutputLength()
        {
            var input = new string('\n', 100).ToCharArray();
            var output = new char[NewlineTranslator.MaxOutputLength(input.Length)];

            int n = new NewlineTranslator(ReceiveNewline.Lf).Translate(input, input.Length, output);

            Assert.Equal(200, n);
        }

        private static string Translate(ReceiveNewline mode, params string[] reads)
        {
            var translator = new NewlineTranslator(mode);
            var result = new StringBuilder();
            foreach (string read in reads)
            {
                char[] input = read.ToCharArray();
                var output = new char[NewlineTranslator.MaxOutputLength(input.Length)];
                int n = translator.Translate(input, input.Length, output);
                result.Append(output, 0, n);
            }

            return result.ToString();
        }
    }
}
