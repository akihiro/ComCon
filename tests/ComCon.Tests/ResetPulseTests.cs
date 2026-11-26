namespace ComCon.Tests
{
    using System.Collections.Generic;
    using Xunit;

    public class ResetPulseTests
    {
        [Fact]
        public void SequenceWithRtsControl()
        {
            var lines = new RecordingLines(dtr: true, rts: true);

            ResetPulse.Send(lines, true, lines.Sleep);

            Assert.Equal(
                new[]
                {
                    "DTR=0", "RTS=1",                        // ESP32 EN low (reset), IO0 high
                    "sleep " + ResetPulse.HoldMilliseconds,
                    "RTS=0",                                 // EN high, IO0 high: normal boot
                    "sleep " + ResetPulse.BootSampleMilliseconds,
                    "DTR=1", "RTS=1",                        // DTR first; Arduino resets on DTR assert
                },
                lines.Log);
        }

        [Fact]
        public void LinesAreRestored()
        {
            var lines = new RecordingLines(dtr: false, rts: true);

            ResetPulse.Send(lines, true, lines.Sleep);

            Assert.False(lines.Dtr);
            Assert.True(lines.Rts);
        }

        [Fact]
        public void Esp32NeverSeesEnLowAfterTheHold()
        {
            // EN is low exactly while RTS is asserted and DTR is not. That must happen only once (no double reset).
            var lines = new RecordingLines(dtr: true, rts: true);

            ResetPulse.Send(lines, true, lines.Sleep);

            Assert.Equal(1, lines.EnLowPeriods);
        }

        [Fact]
        public void Esp32BootsWithIo0High()
        {
            // IO0 is low while DTR is asserted and RTS is not. It must be high when EN is released.
            var lines = new RecordingLines(dtr: true, rts: true);

            ResetPulse.Send(lines, true, lines.Sleep);

            Assert.False(lines.Io0LowWhenEnReleased);
        }

        [Fact]
        public void WithoutRtsControlOnlyDtrIsPulsed()
        {
            var lines = new RecordingLines(dtr: true, rts: true);

            ResetPulse.Send(lines, false, lines.Sleep);

            Assert.DoesNotContain(lines.Log, e => e.StartsWith("RTS"));
            Assert.Equal("DTR=0", lines.Log[0]);
            Assert.Equal("DTR=1", lines.Log[lines.Log.Count - 1]);
        }

        /// <summary>Records line changes and simulates the ESP32 auto-reset transistors.</summary>
        private sealed class RecordingLines : IModemLines
        {
            private bool dtr;
            private bool rts;

            public RecordingLines(bool dtr, bool rts)
            {
                this.dtr = dtr;
                this.rts = rts;
            }

            public List<string> Log { get; } = new List<string>();

            public int EnLowPeriods { get; private set; }

            public bool Io0LowWhenEnReleased { get; private set; }

            public bool Dtr
            {
                get { return dtr; }
                set { Change(value, rts); Log.Add("DTR=" + (value ? 1 : 0)); }
            }

            public bool Rts
            {
                get { return rts; }
                set { Change(dtr, value); Log.Add("RTS=" + (value ? 1 : 0)); }
            }

            public void Sleep(int ms)
            {
                Log.Add("sleep " + ms);
            }

            private static bool EnLow(bool dtr, bool rts)
            {
                return rts && !dtr;
            }

            private static bool Io0Low(bool dtr, bool rts)
            {
                return dtr && !rts;
            }

            private void Change(bool newDtr, bool newRts)
            {
                bool wasLow = EnLow(dtr, rts);
                bool isLow = EnLow(newDtr, newRts);
                if (!wasLow && isLow)
                {
                    EnLowPeriods++;
                }

                if (wasLow && !isLow && Io0Low(newDtr, newRts))
                {
                    Io0LowWhenEnReleased = true;
                }

                dtr = newDtr;
                rts = newRts;
            }
        }
    }
}
