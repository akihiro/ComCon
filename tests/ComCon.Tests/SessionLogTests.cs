namespace ComCon.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using Xunit;

    public class SessionLogTests
    {
        private static readonly DateTime Time = new DateTime(2026, 11, 26, 13, 45, 1, 123);

        [Fact]
        public void WithoutTimestampsTextIsWrittenUnchanged()
        {
            Assert.Equal("boot\r\n\x1b[32mok\x1b[0m\r\n", Log(false, "boot\r\n", "\x1b[32mok\x1b[0m\r\n"));
        }

        [Fact]
        public void EachLineGetsATimestamp()
        {
            Assert.Equal(
                "[2026-11-26 13:45:01.123] one\r\n[2026-11-26 13:45:01.123] two\n",
                Log(true, "one\r\ntwo\n"));
        }

        [Fact]
        public void LineSplitAcrossWritesIsStampedOnce()
        {
            Assert.Equal("[2026-11-26 13:45:01.123] hello world\n", Log(true, "hel", "lo wor", "ld\n"));
        }

        [Fact]
        public void LineIsStampedWhenItsFirstCharacterArrives()
        {
            // No timestamp is written for a line that has not started yet.
            Assert.Equal("[2026-11-26 13:45:01.123] a\n", Log(true, "a\n"));
        }

        [Fact]
        public void TimestampUsesTheClockAtLineStart()
        {
            var writer = new StringWriter();
            DateTime now = Time;
            var log = new SessionLog(writer, true, () => now);

            Write(log, "first ");
            now = Time.AddSeconds(5);
            Write(log, "still first\nsecond\n");

            Assert.Equal("[2026-11-26 13:45:01.123] first still first\n[2026-11-26 13:45:06.123] second\n", writer.ToString());
        }

        [Fact]
        public void WritesAfterDisposeAreIgnored()
        {
            var writer = new StringWriter();
            var log = new SessionLog(writer, false, () => Time);
            Write(log, "a");
            log.Dispose();

            Write(log, "b");
            log.Dispose();
        }

        [Fact]
        public void OpenAppendsToTheFileInTheGivenEncoding()
        {
            string path = Path.Combine(Path.GetTempPath(), "ComCon.Tests." + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                File.WriteAllText(path, "old\n", new UTF8Encoding(false));
                using (SessionLog log = SessionLog.Open(path, new UTF8Encoding(false), false))
                {
                    Write(log, "新しい行\n");

                    // The file can be read while ComCon has it open (e.g. to follow the log).
                    using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        Assert.True(reader.Length > 0);
                    }
                }

                byte[] bytes = File.ReadAllBytes(path);
                Assert.Equal("old\n新しい行\n", new UTF8Encoding(false).GetString(bytes));
                Assert.NotEqual(0xEF, bytes[0]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string Log(bool timestamps, params string[] writes)
        {
            var writer = new StringWriter();
            using (var log = new SessionLog(writer, timestamps, () => Time))
            {
                foreach (string s in writes)
                {
                    Write(log, s);
                }

                return writer.ToString();
            }
        }

        private static void Write(SessionLog log, string s)
        {
            char[] chars = s.ToCharArray();
            log.Write(chars, chars.Length);
        }
    }
}
