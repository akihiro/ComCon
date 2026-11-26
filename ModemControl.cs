namespace ComCon
{
    using System;
    using System.IO.Ports;

    /// <summary>The DTR and RTS output lines of a port. Abstracted so the reset sequence can be tested.</summary>
    internal interface IModemLines
    {
        bool Dtr { get; set; }

        bool Rts { get; set; }
    }

    /// <summary>DTR/RTS of a real <see cref="SerialPort"/>.</summary>
    internal sealed class SerialPortModemLines : IModemLines
    {
        private readonly SerialPort port;

        public SerialPortModemLines(SerialPort port)
        {
            this.port = port;
        }

        public bool Dtr
        {
            get { return port.DtrEnable; }
            set { port.DtrEnable = value; }
        }

        public bool Rts
        {
            get { return port.RtsEnable; }
            set { port.RtsEnable = value; }
        }
    }

    /// <summary>
    /// The reset pulse sent by Ctrl+] R. One sequence covers the two common auto-reset circuits:
    /// <list type="bullet">
    /// <item>ESP32/ESP8266 boards (two transistors): EN is pulled low while RTS is asserted and DTR is not;
    /// IO0 is pulled low while DTR is asserted and RTS is not. Both asserted (ComCon's default) or both
    /// deasserted leave EN and IO0 high.</item>
    /// <item>Arduino boards with a capacitor on DTR: the MCU resets when DTR goes from deasserted to asserted.</item>
    /// </list>
    /// </summary>
    internal static class ResetPulse
    {
        /// <summary>How long EN is held low (ESP32).</summary>
        public const int HoldMilliseconds = 100;

        /// <summary>Time for the ESP32 to sample IO0 after EN goes high, before IO0 may change.</summary>
        public const int BootSampleMilliseconds = 50;

        /// <summary>
        /// Sends the pulse and restores the lines to their state before the call.
        /// When <paramref name="rtsControllable"/> is false (RTS/CTS flow control owns RTS), only DTR is pulsed.
        /// </summary>
        public static void Send(IModemLines lines, bool rtsControllable, Action<int> sleep)
        {
            bool dtr = lines.Dtr;
            bool rts = rtsControllable && lines.Rts;

            // 1. ESP32: RTS asserted, DTR not -> EN low (reset), IO0 high.
            lines.Dtr = false;
            if (rtsControllable)
            {
                lines.Rts = true;
            }

            sleep(HoldMilliseconds);

            // 2. ESP32: both deasserted -> EN high with IO0 high, so it boots normally (not into the bootloader).
            if (rtsControllable)
            {
                lines.Rts = false;
            }

            sleep(BootSampleMilliseconds);

            // 3. Restore DTR before RTS. Restoring RTS first while DTR is still deasserted would pull EN low again
            //    and reset the ESP32 a second time. Arduino: DTR going back to asserted is what resets it.
            lines.Dtr = dtr;
            if (rtsControllable)
            {
                lines.Rts = rts;
            }
        }
    }
}
