namespace ComCon
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Runs the Windows console in raw mode.
    /// On input, ENABLE_VIRTUAL_TERMINAL_INPUT delivers arrow keys and similar keys as VT sequences.
    /// On output, ENABLE_VIRTUAL_TERMINAL_PROCESSING lets Windows Terminal render the VT sequences received from the port.
    /// When stdin or stdout is redirected, bytes are read and written unchanged.
    /// </summary>
    internal sealed class TerminalConsole : IDisposable
    {
        private readonly object sync = new object();
        private readonly IntPtr inputHandle;
        private readonly IntPtr outputHandle;
        private readonly uint originalInputMode;
        private readonly uint originalOutputMode;
        private readonly bool inputIsConsole;
        private readonly bool outputIsConsole;
        private readonly Stream rawInput;
        private readonly Stream rawOutput;
        private bool restored;

        public TerminalConsole()
        {
            inputHandle = NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE);
            outputHandle = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
            inputIsConsole = NativeMethods.GetConsoleMode(inputHandle, out originalInputMode);
            outputIsConsole = NativeMethods.GetConsoleMode(outputHandle, out originalOutputMode);

            if (outputIsConsole)
            {
                uint mode = originalOutputMode
                    | NativeMethods.ENABLE_PROCESSED_OUTPUT
                    | NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING
                    | NativeMethods.DISABLE_NEWLINE_AUTO_RETURN;
                if (!NativeMethods.SetConsoleMode(outputHandle, mode))
                {
                    throw new InvalidOperationException(Strings.VtOutputUnsupported);
                }
            }
            else
            {
                rawOutput = Console.OpenStandardOutput();
            }

            if (inputIsConsole)
            {
                uint mode = (originalInputMode
                    & ~(NativeMethods.ENABLE_PROCESSED_INPUT
                        | NativeMethods.ENABLE_LINE_INPUT
                        | NativeMethods.ENABLE_ECHO_INPUT
                        | NativeMethods.ENABLE_WINDOW_INPUT
                        | NativeMethods.ENABLE_MOUSE_INPUT))
                    | NativeMethods.ENABLE_VIRTUAL_TERMINAL_INPUT;
                if (!NativeMethods.SetConsoleMode(inputHandle, mode))
                {
                    Restore();
                    throw new InvalidOperationException(Strings.VtInputUnsupported);
                }
            }
            else
            {
                rawInput = Console.OpenStandardInput();
            }
        }

        /// <summary>Whether input comes interactively from the keyboard (stdin is a console).</summary>
        public bool InputIsConsole
        {
            get { return inputIsConsole; }
        }

        /// <summary>Whether output goes to a console. False means it is redirected to a file or pipe.</summary>
        public bool OutputIsConsole
        {
            get { return outputIsConsole; }
        }

        /// <summary>Reads keystrokes as characters. Blocks until input arrives and returns 0 on failure.</summary>
        public int ReadChars(char[] buffer)
        {
            uint read;
            if (!NativeMethods.ReadConsoleW(inputHandle, buffer, (uint)buffer.Length, out read, IntPtr.Zero))
            {
                return 0;
            }

            return (int)read;
        }

        /// <summary>Reads bytes from redirected stdin. Returns 0 at EOF.</summary>
        public int ReadBytes(byte[] buffer)
        {
            return rawInput.Read(buffer, 0, buffer.Length);
        }

        public void WriteChars(char[] buffer, int count)
        {
            if (count <= 0)
            {
                return;
            }

            lock (sync)
            {
                if (outputIsConsole)
                {
                    uint written;
                    NativeMethods.WriteConsoleW(outputHandle, buffer, (uint)count, out written, IntPtr.Zero);
                }
                else
                {
                    var bytes = Console.OutputEncoding.GetBytes(buffer, 0, count);
                    rawOutput.Write(bytes, 0, bytes.Length);
                    rawOutput.Flush();
                }
            }
        }

        public void WriteText(string text)
        {
            var chars = text.ToCharArray();
            WriteChars(chars, chars.Length);
        }

        /// <summary>When output is redirected, writes received data as raw bytes.</summary>
        public void WriteBytes(byte[] buffer, int count)
        {
            lock (sync)
            {
                rawOutput.Write(buffer, 0, count);
                rawOutput.Flush();
            }
        }

        /// <summary>Writes one of ComCon's own messages as a single line in reverse video.</summary>
        public void WriteStatus(string message)
        {
            if (outputIsConsole)
            {
                WriteText("\r\n\x1b[7m[ComCon] " + message + "\x1b[0m\r\n");
            }
            else
            {
                // When output is redirected, use stderr so the received data stays clean.
                Console.Error.WriteLine("[ComCon] " + message);
            }
        }

        /// <summary>Restores the console modes to their state at startup. Safe to call more than once.</summary>
        public void Restore()
        {
            lock (sync)
            {
                if (restored)
                {
                    return;
                }

                restored = true;
                if (inputIsConsole)
                {
                    NativeMethods.SetConsoleMode(inputHandle, originalInputMode);
                }

                if (outputIsConsole)
                {
                    NativeMethods.SetConsoleMode(outputHandle, originalOutputMode);
                }
            }
        }

        public void Dispose()
        {
            Restore();
        }
    }
}
