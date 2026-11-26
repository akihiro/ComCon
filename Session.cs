namespace ComCon
{
    using System;
    using System.IO.Ports;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Connects a serial port and the console in both directions.
    /// A dedicated thread reads from SerialPort.BaseStream and writes to the console.
    /// The main thread reads keystrokes, handles local commands that start with Ctrl+], and writes the rest to the port.
    /// </summary>
    internal sealed class Session
    {
        private const char EscapeChar = '\x1d'; // Ctrl+]
        private const int BreakMilliseconds = 250;

        private readonly Options options;
        private readonly SerialPort port;
        private readonly Decoder decoder;
        private readonly Encoder encoder;
        private TerminalConsole term;
        private volatile bool stopping;
        private bool escapePending;
        private bool localEcho;

        public Session(Options options)
        {
            this.options = options;
            localEcho = options.LocalEcho;
            decoder = options.Encoding.GetDecoder();
            encoder = options.Encoding.GetEncoder();

            port = new SerialPort(options.PortName, options.BaudRate, options.Parity, options.DataBits, options.StopBits)
            {
                Handshake = options.Handshake,
                DtrEnable = options.DtrEnable,
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 3000,
            };

            // With RTS/CTS flow control the driver owns RTS, and setting it by hand throws.
            if (options.Handshake == Handshake.None || options.Handshake == Handshake.XOnXOff)
            {
                port.RtsEnable = options.RtsEnable;
            }
        }

        public int Run()
        {
            port.Open();

            // After a USB-serial device is unplugged, the .NET Framework SerialStream finalizer
            // can throw and crash the process, so suppress finalization.
            // This is safe because Close() disposes the stream explicitly.
            GC.SuppressFinalize(port.BaseStream);

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

                term.WriteStatus(Localization.Format(Strings.Connected, options.Summary));

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
            try
            {
                port.BreakState = true;
                Thread.Sleep(BreakMilliseconds);
                port.BreakState = false;
                term.WriteStatus(Strings.BreakSent);
            }
            catch (Exception ex)
            {
                term.WriteStatus(Localization.Format(Strings.BreakFailed, ex.Message));
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

            try
            {
                port.Write(bytes, 0, count);
            }
            catch (TimeoutException)
            {
                term.WriteStatus(Strings.WriteTimeout);
            }
            catch (Exception ex)
            {
                Fatal(Localization.Format(Strings.WriteError, ex.Message));
            }
        }

        private void ReceiveLoop()
        {
            var buffer = new byte[4096];
            var chars = new char[options.Encoding.GetMaxCharCount(buffer.Length)];

            while (!stopping)
            {
                int n;
                try
                {
                    n = port.BaseStream.Read(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    if (!stopping)
                    {
                        Fatal(Localization.Format(Strings.ReadError, ex.Message));
                    }

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
                    term.WriteChars(chars, c);
                }
                else
                {
                    term.WriteBytes(buffer, n);
                }
            }
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
            Environment.Exit(2);
        }

        private void ClosePort()
        {
            try
            {
                port.Close();
            }
            catch (Exception)
            {
                // Close can fail on a device that is already disconnected.
            }
        }
    }
}
