namespace ComCon
{
    using System;
    using System.IO;
    using System.IO.Ports;
    using System.Linq;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Connects a serial port and the console in both directions.
    /// A dedicated thread reads from SerialPort.BaseStream and writes to the console.
    /// The main thread reads keystrokes, handles local commands that start with Ctrl+], and writes the rest to the port.
    /// With --reconnect, the receiver thread also reopens the port after it disappears (USB unplug, device reset).
    /// </summary>
    internal sealed class Session
    {
        private const char EscapeChar = '\x1d'; // Ctrl+]
        private const int BreakMilliseconds = 250;
        private const int ReconnectPollMilliseconds = 500;

        private readonly Options options;
        private readonly Decoder decoder;
        private readonly Encoder encoder;

        // Guards "port". The receiver thread replaces it on reconnect; the main thread writes through it.
        private readonly object portLock = new object();
        private SerialPort port;

        private TerminalConsole term;
        private volatile bool stopping;
        private bool escapePending;
        private bool localEcho;
        private bool notConnectedShown;

        // Current DTR/RTS output state, changed by Ctrl+] d / r and reapplied when --reconnect reopens the port.
        private volatile bool dtr;
        private volatile bool rts;

        // Written by the receiver thread; set to null when logging stops (write error or exit).
        private volatile SessionLog log;

        public Session(Options options)
        {
            this.options = options;
            localEcho = options.LocalEcho;
            dtr = options.DtrEnable;
            rts = options.RtsEnable;
            decoder = options.Encoding.GetDecoder();
            encoder = options.Encoding.GetEncoder();
        }

        public int Run()
        {
            if (options.LogFile != null)
            {
                try
                {
                    log = SessionLog.Open(options.LogFile, options.Encoding, options.LogTimestamp);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
                    || ex is NotSupportedException || ex is System.Security.SecurityException)
                {
                    // Reported here: Program would otherwise describe these exceptions as port errors.
                    Console.Error.WriteLine(Localization.Format(Strings.CannotOpenLog, options.LogFile, ex.Message));
                    return 2;
                }
            }

            try
            {
                return RunSession();
            }
            finally
            {
                CloseLog();
            }
        }

        private int RunSession()
        {
            string initialError = null;
            try
            {
                port = OpenPort();
            }
            catch (Exception ex) when (options.Reconnect && (ex is IOException || ex is UnauthorizedAccessException))
            {
                // With --reconnect, a missing or busy port at startup is waited for like a lost one.
                initialError = ex.Message;
            }

            using (term = new TerminalConsole())
            {
                try
                {
                    Console.Title = "ComCon - " + options.Summary;
                }
                catch (Exception)
                {
                    // Failing to set the title is harmless.
                }

                if (port != null)
                {
                    term.WriteStatus(Localization.Format(Strings.Connected, options.Summary));
                }
                else
                {
                    term.WriteStatus(Localization.Format(Strings.WaitingForPort, options.PortName, initialError));
                }

                if (log != null)
                {
                    term.WriteStatus(Localization.Format(Strings.Logging, Path.GetFullPath(options.LogFile)));
                }

                var receiver = new Thread(ReceiveLoop) { IsBackground = true, Name = "ComCon receiver" };
                receiver.Start();

                if (term.InputIsConsole)
                {
                    InteractiveLoop();
                }
                else
                {
                    PipeLoop();
                }

                stopping = true;
                ClosePort();
                term.WriteStatus(Strings.Disconnected);
            }

            return 0;
        }

        /// <summary>Creates and opens a port with the configured settings. Throws if it cannot be opened.</summary>
        private SerialPort OpenPort()
        {
            var p = new SerialPort(options.PortName, options.BaudRate, options.Parity, options.DataBits, options.StopBits)
            {
                Handshake = options.Handshake,
                DtrEnable = dtr,
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 3000,
            };

            // With RTS/CTS flow control the driver owns RTS, and setting it by hand throws.
            if (RtsControllable)
            {
                p.RtsEnable = rts;
            }

            try
            {
                p.Open();
            }
            catch
            {
                p.Dispose();
                throw;
            }

            // After a USB-serial device is unplugged, the .NET Framework SerialStream finalizer
            // can throw and crash the process, so suppress finalization.
            // This is safe because Close() disposes the stream explicitly.
            GC.SuppressFinalize(p.BaseStream);
            return p;
        }

        /// <summary>With RTS/CTS flow control the driver owns RTS, and setting it by hand throws.</summary>
        private bool RtsControllable
        {
            get { return options.Handshake == Handshake.None || options.Handshake == Handshake.XOnXOff; }
        }

        private SerialPort CurrentPort
        {
            get
            {
                lock (portLock)
                {
                    return port;
                }
            }
        }

        private void InteractiveLoop()
        {
            var input = new char[1024];
            var pending = new StringBuilder();

            while (!stopping)
            {
                int n = term.ReadChars(input);
                if (n <= 0)
                {
                    break;
                }

                for (int i = 0; i < n && !stopping; i++)
                {
                    char c = input[i];
                    if (escapePending)
                    {
                        escapePending = false;
                        if (c == EscapeChar)
                        {
                            pending.Append(EscapeChar);
                        }
                        else
                        {
                            // Send the input so far before running the command, to keep the order.
                            Send(pending);
                            ExecuteCommand(c);
                        }
                    }
                    else if (c == EscapeChar)
                    {
                        escapePending = true;
                    }
                    else if (c == '\r')
                    {
                        pending.Append(options.NewLine);
                    }
                    else
                    {
                        pending.Append(c);
                    }
                }

                Send(pending);
            }
        }

        /// <summary>For redirected stdin: sends its contents to the port unchanged and returns at EOF.</summary>
        private void PipeLoop()
        {
            var buffer = new byte[4096];
            int n;
            while (!stopping && (n = term.ReadBytes(buffer)) > 0)
            {
                WriteToPort(buffer, n);
            }
        }

        private void ExecuteCommand(char c)
        {
            switch (c)
            {
                case 'q':
                case 'Q':
                case '.':
                    stopping = true;
                    break;
                case 'b':
                case 'B':
                    SendBreak();
                    break;
                case 'e':
                case 'E':
                    localEcho = !localEcho;
                    term.WriteStatus(localEcho ? Strings.LocalEchoOn : Strings.LocalEchoOff);
                    break;
                case 'd':
                case 'D':
                    ToggleDtr();
                    break;
                case 'r':
                    ToggleRts();
                    break;
                case 'R':
                    SendResetPulse();
                    break;
                case '?':
                case 'h':
                case 'H':
                    term.WriteStatus(Localization.Format(Strings.CommandHelp, options.Summary));
                    break;
                default:
                    term.WriteStatus(Strings.UnknownCommand);
                    break;
            }
        }

        private void SendBreak()
        {
            SerialPort p = CurrentPort;
            if (p == null)
            {
                term.WriteStatus(Strings.NotConnected);
                return;
            }

            try
            {
                p.BreakState = true;
                Thread.Sleep(BreakMilliseconds);
                p.BreakState = false;
                term.WriteStatus(Strings.BreakSent);
            }
            catch (Exception ex)
            {
                term.WriteStatus(Localization.Format(Strings.BreakFailed, ex.Message));
            }
        }

        private void ToggleDtr()
        {
            SerialPort p = CurrentPort;
            if (p == null)
            {
                term.WriteStatus(Strings.NotConnected);
                return;
            }

            try
            {
                p.DtrEnable = !dtr;
                dtr = !dtr;
                term.WriteStatus(dtr ? Strings.DtrOn : Strings.DtrOff);
            }
            catch (Exception ex)
            {
                term.WriteStatus(Localization.Format(Strings.ModemLinesFailed, ex.Message));
            }
        }

        private void ToggleRts()
        {
            if (!RtsControllable)
            {
                term.WriteStatus(Strings.RtsOwnedByFlowControl);
                return;
            }

            SerialPort p = CurrentPort;
            if (p == null)
            {
                term.WriteStatus(Strings.NotConnected);
                return;
            }

            try
            {
                p.RtsEnable = !rts;
                rts = !rts;
                term.WriteStatus(rts ? Strings.RtsOn : Strings.RtsOff);
            }
            catch (Exception ex)
            {
                term.WriteStatus(Localization.Format(Strings.ModemLinesFailed, ex.Message));
            }
        }

        /// <summary>Resets the board through DTR/RTS (ESP32/ESP8266 auto-reset, Arduino DTR reset).</summary>
        private void SendResetPulse()
        {
            SerialPort p = CurrentPort;
            if (p == null)
            {
                term.WriteStatus(Strings.NotConnected);
                return;
            }

            try
            {
                ResetPulse.Send(new SerialPortModemLines(p), RtsControllable, Thread.Sleep);
                term.WriteStatus(RtsControllable ? Strings.ResetSent : Strings.ResetSentDtrOnly);
            }
            catch (Exception ex)
            {
                term.WriteStatus(Localization.Format(Strings.ModemLinesFailed, ex.Message));
            }
        }

        private void Send(StringBuilder pending)
        {
            if (pending.Length == 0)
            {
                return;
            }

            var chars = pending.ToString().ToCharArray();
            pending.Clear();

            // The Encoder keeps state, so a surrogate pair split across reads is still encoded correctly.
            var bytes = new byte[encoder.GetByteCount(chars, 0, chars.Length, false)];
            int count = encoder.GetBytes(chars, 0, chars.Length, bytes, 0, false);
            WriteToPort(bytes, count);

            if (localEcho)
            {
                string shown = new string(chars).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
                term.WriteText(shown);
            }
        }

        private void WriteToPort(byte[] bytes, int count)
        {
            if (count <= 0)
            {
                return;
            }

            SerialPort p = CurrentPort;
            if (p == null)
            {
                // Only while waiting for --reconnect. Say so once per outage instead of on every keystroke.
                if (!notConnectedShown)
                {
                    notConnectedShown = true;
                    term.WriteStatus(Strings.NotConnected);
                }

                return;
            }

            try
            {
                p.Write(bytes, 0, count);
            }
            catch (TimeoutException)
            {
                term.WriteStatus(Strings.WriteTimeout);
            }
            catch (Exception ex)
            {
                if (stopping)
                {
                    return;
                }

                if (options.Reconnect)
                {
                    PortLost(p, ex.Message);
                }
                else
                {
                    Fatal(Localization.Format(Strings.WriteError, ex.Message));
                }
            }
        }

        private void ReceiveLoop()
        {
            var buffer = new byte[4096];
            var chars = new char[options.Encoding.GetMaxCharCount(buffer.Length)];
            var newlines = new NewlineTranslator(options.ReceiveNewline);
            var translated = new char[NewlineTranslator.MaxOutputLength(chars.Length)];

            while (!stopping)
            {
                SerialPort p = CurrentPort;
                if (p == null)
                {
                    if (!TryReconnect())
                    {
                        Thread.Sleep(ReconnectPollMilliseconds);
                    }

                    continue;
                }

                int n;
                try
                {
                    n = p.BaseStream.Read(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    if (stopping)
                    {
                        return;
                    }

                    if (options.Reconnect)
                    {
                        PortLost(p, ex.Message);
                        continue;
                    }

                    Fatal(Localization.Format(Strings.ReadError, ex.Message));
                    return;
                }

                if (n <= 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                if (term.OutputIsConsole)
                {
                    int c = decoder.GetChars(buffer, 0, n, chars, 0, false);
                    char[] shown = chars;
                    if (newlines.Mode != ReceiveNewline.None)
                    {
                        c = newlines.Translate(chars, c, translated);
                        shown = translated;
                    }

                    term.WriteChars(shown, c);
                    WriteLog(shown, c);
                }
                else
                {
                    term.WriteBytes(buffer, n);
                    if (log != null)
                    {
                        // Pipe mode passes raw bytes through, but the log is text, so decode for it.
                        WriteLog(chars, decoder.GetChars(buffer, 0, n, chars, 0, false));
                    }
                }
            }
        }

        /// <summary>
        /// Called from either thread when the port fails with --reconnect. Closes it and lets the receiver
        /// thread wait for it to come back. Only the first caller for a given port reports the loss.
        /// </summary>
        private void PortLost(SerialPort lost, string reason)
        {
            lock (portLock)
            {
                if (!ReferenceEquals(port, lost))
                {
                    return;
                }

                port = null;
            }

            CloseQuietly(lost);
            term.WriteStatus(Localization.Format(Strings.PortLost, options.PortName, reason));
        }

        /// <summary>Tries once to reopen the port. Returns true when connected.</summary>
        private bool TryReconnect()
        {
            // Opening a port that does not exist is slow on some drivers, so check that it is present first.
            if (!SerialPort.GetPortNames().Contains(options.PortName, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            SerialPort p;
            try
            {
                p = OpenPort();
            }
            catch (Exception)
            {
                // The device may still be enumerating, or another program grabbed it. Try again later.
                // Catch everything: an exception escaping this background thread would end the process.
                return false;
            }

            lock (portLock)
            {
                if (stopping)
                {
                    CloseQuietly(p);
                    return false;
                }

                port = p;
                notConnectedShown = false;
            }

            // Drop any partial multi-byte character left over from before the outage.
            // This runs on the receiver thread, which owns the decoder.
            decoder.Reset();
            term.WriteStatus(Localization.Format(Strings.Reconnected, options.Summary));
            return true;
        }

        /// <summary>
        /// Handles an unrecoverable error. The main thread is blocked waiting for a keystroke,
        /// so restore the console and then exit the whole process.
        /// </summary>
        private void Fatal(string message)
        {
            stopping = true;
            term.WriteStatus(message);
            term.Restore();
            ClosePort();
            CloseLog();
            Environment.Exit(2);
        }

        /// <summary>Writes received text to the log, if any. On a write error, reports it once and stops logging.</summary>
        private void WriteLog(char[] buffer, int count)
        {
            SessionLog current = log;
            if (current == null)
            {
                return;
            }

            try
            {
                current.Write(buffer, count);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
            {
                CloseLog();
                term.WriteStatus(Localization.Format(Strings.LogWriteError, ex.Message));
            }
        }

        private void CloseLog()
        {
            SessionLog current = log;
            log = null;
            if (current != null)
            {
                current.Dispose();
            }
        }

        private void ClosePort()
        {
            SerialPort p;
            lock (portLock)
            {
                p = port;
                port = null;
            }

            CloseQuietly(p);
        }

        private static void CloseQuietly(SerialPort p)
        {
            if (p == null)
            {
                return;
            }

            try
            {
                p.Close();
            }
            catch (Exception)
            {
                // Close can fail on a device that is already disconnected.
            }
        }
    }
}
