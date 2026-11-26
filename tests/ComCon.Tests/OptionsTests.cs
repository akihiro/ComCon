namespace ComCon.Tests
{
    using System;
    using System.IO.Ports;
    using Xunit;

    public class OptionsTests
    {
        [Fact]
        public void Defaults()
        {
            Options o = Options.Parse(new[] { "COM3" });

            Assert.Equal("COM3", o.PortName);
            Assert.Equal(115200, o.BaudRate);
            Assert.Equal(8, o.DataBits);
            Assert.Equal(Parity.None, o.Parity);
            Assert.Equal(StopBits.One, o.StopBits);
            Assert.Equal(Handshake.None, o.Handshake);
            Assert.True(o.DtrEnable);
            Assert.True(o.RtsEnable);
            Assert.False(o.LocalEcho);
            Assert.Equal("\r", o.NewLine);
            Assert.Equal(65001, o.Encoding.CodePage);
            Assert.Empty(o.Encoding.GetPreamble());
            Assert.False(o.ShowHelp);
            Assert.False(o.ShowVersion);
            Assert.False(o.ListPorts);
        }

        [Theory]
        [InlineData("COM3", "COM3")]
        [InlineData("com3", "COM3")]
        [InlineData("3", "COM3")]
        [InlineData("12", "COM12")]
        public void PortNameIsNormalized(string arg, string expected)
        {
            Assert.Equal(expected, Options.Parse(new[] { arg }).PortName);
        }

        [Fact]
        public void NoArgumentsLeavesPortUnset()
        {
            Assert.Null(Options.Parse(new string[0]).PortName);
        }

        [Fact]
        public void ShortOptions()
        {
            Options o = Options.Parse(new[]
            {
                "COM5", "-b", "9600", "-d", "7", "-p", "even", "-s", "2", "-f", "rtscts",
                "-n", "crlf", "-e", "--no-dtr", "--no-rts", "--encoding", "shift_jis",
            });

            Assert.Equal("COM5", o.PortName);
            Assert.Equal(9600, o.BaudRate);
            Assert.Equal(7, o.DataBits);
            Assert.Equal(Parity.Even, o.Parity);
            Assert.Equal(StopBits.Two, o.StopBits);
            Assert.Equal(Handshake.RequestToSend, o.Handshake);
            Assert.Equal("\r\n", o.NewLine);
            Assert.True(o.LocalEcho);
            Assert.False(o.DtrEnable);
            Assert.False(o.RtsEnable);
            Assert.Equal(932, o.Encoding.CodePage);
        }

        [Fact]
        public void LongOptions()
        {
            Options o = Options.Parse(new[]
            {
                "--baud", "57600", "--data", "5", "--parity", "odd", "--stop", "1.5", "--flow", "xonxoff",
                "--newline", "lf", "--echo", "COM1",
            });

            Assert.Equal(57600, o.BaudRate);
            Assert.Equal(5, o.DataBits);
            Assert.Equal(Parity.Odd, o.Parity);
            Assert.Equal(StopBits.OnePointFive, o.StopBits);
            Assert.Equal(Handshake.XOnXOff, o.Handshake);
            Assert.Equal("\n", o.NewLine);
            Assert.True(o.LocalEcho);
            Assert.Equal("COM1", o.PortName);
        }

        [Theory]
        [InlineData("none", Parity.None)]
        [InlineData("n", Parity.None)]
        [InlineData("ODD", Parity.Odd)]
        [InlineData("e", Parity.Even)]
        [InlineData("mark", Parity.Mark)]
        [InlineData("s", Parity.Space)]
        public void ParityValues(string value, Parity expected)
        {
            Assert.Equal(expected, Options.Parse(new[] { "-p", value }).Parity);
        }

        [Theory]
        [InlineData("none", Handshake.None)]
        [InlineData("rtscts", Handshake.RequestToSend)]
        [InlineData("hw", Handshake.RequestToSend)]
        [InlineData("hardware", Handshake.RequestToSend)]
        [InlineData("xonxoff", Handshake.XOnXOff)]
        [InlineData("sw", Handshake.XOnXOff)]
        [InlineData("software", Handshake.XOnXOff)]
        [InlineData("both", Handshake.RequestToSendXOnXOff)]
        public void FlowControlValues(string value, Handshake expected)
        {
            Assert.Equal(expected, Options.Parse(new[] { "-f", value }).Handshake);
        }

        [Theory]
        [InlineData("utf-8")]
        [InlineData("UTF8")]
        public void Utf8EncodingHasNoBom(string value)
        {
            Options o = Options.Parse(new[] { "--encoding", value });

            Assert.Equal(65001, o.Encoding.CodePage);
            Assert.Empty(o.Encoding.GetPreamble());
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("--help")]
        [InlineData("/?")]
        public void HelpFlags(string arg)
        {
            Assert.True(Options.Parse(new[] { arg }).ShowHelp);
        }

        [Theory]
        [InlineData("-v")]
        [InlineData("--version")]
        public void VersionFlags(string arg)
        {
            Assert.True(Options.Parse(new[] { arg }).ShowVersion);
        }

        [Theory]
        [InlineData("-l")]
        [InlineData("--list")]
        public void ListFlags(string arg)
        {
            Assert.True(Options.Parse(new[] { arg }).ListPorts);
        }

        [Fact]
        public void LanguageOption()
        {
            Assert.Equal("ja", Options.Parse(new[] { "--lang", "ja" }).Language.Name);
        }

        [Fact]
        public void SummaryWithDefaults()
        {
            Assert.Equal("COM3 115200 8N1", Options.Parse(new[] { "COM3" }).Summary);
        }

        [Fact]
        public void SummaryWithSettings()
        {
            Assert.Equal("COM4 9600 7E2", Options.Parse(new[] { "COM4", "-b", "9600", "-d", "7", "-p", "even", "-s", "2" }).Summary);
            Assert.Equal("COM1 115200 8M1.5", Options.Parse(new[] { "COM1", "-p", "mark", "-s", "1.5" }).Summary);
        }

        [Theory]
        [InlineData("--bogus")]
        [InlineData("-x")]
        public void UnknownOptionThrows(string arg)
        {
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { arg }));
        }

        [Fact]
        public void MultiplePortsThrow()
        {
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "COM3", "COM4" }));
        }

        [Theory]
        [InlineData("-b")]
        [InlineData("--parity")]
        [InlineData("--encoding")]
        [InlineData("--lang")]
        public void MissingValueThrows(string option)
        {
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "COM3", option }));
        }

        [Theory]
        [InlineData("-b", "0")]
        [InlineData("-b", "fast")]
        [InlineData("-d", "4")]
        [InlineData("-d", "9")]
        [InlineData("-p", "odd-ish")]
        [InlineData("-s", "3")]
        [InlineData("-f", "dtr")]
        [InlineData("-n", "crcr")]
        [InlineData("--encoding", "no-such-encoding")]
        [InlineData("--lang", "not a culture!")]
        public void InvalidValueThrows(string option, string value)
        {
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "COM3", option, value }));
        }
    }
}
