namespace ComCon
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Writes received data to a log file (--log), optionally with a timestamp at the start of each line
    /// (--log-timestamp). The log gets the same text as the console, escape sequences included.
    /// ComCon's own status messages are not logged. Thread-safe; writes are flushed immediately so the log
    /// survives a crash or an unplugged device.
    /// </summary>
    internal sealed class SessionLog : IDisposable
    {
        private const string TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff] ";

        private readonly object sync = new object();
        private readonly TextWriter writer;
        private readonly bool timestamps;
        private readonly Func<DateTime> clock;
        private bool atLineStart = true;
        private bool disposed;

        public SessionLog(TextWriter writer, bool timestamps, Func<DateTime> clock)
        {
            this.writer = writer;
            this.timestamps = timestamps;
            this.clock = clock;
        }

        /// <summary>Opens (appends to) the log file. Throws IOException and similar if it cannot be opened.</summary>
        public static SessionLog Open(string path, Encoding encoding, bool timestamps)
        {
            // FileShare.Read lets other programs (e.g. Get-Content -Wait) follow the log while ComCon runs.
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, encoding) { AutoFlush = true };
            return new SessionLog(writer, timestamps, () => DateTime.Now);
        }

        /// <summary>Writes received text. Throws IOException if the file cannot be written (e.g. disk full).</summary>
        public void Write(char[] buffer, int count)
        {
            lock (sync)
            {
                if (disposed || count <= 0)
                {
                    return;
                }

                if (!timestamps)
                {
                    writer.Write(buffer, 0, count);
                    return;
                }

                // Write line by line, stamping each line when its first character arrives.
                int start = 0;
                while (start < count)
                {
                    if (atLineStart)
                    {
                        writer.Write(clock().ToString(TimestampFormat, CultureInfo.InvariantCulture));
                        atLineStart = false;
                    }

                    int lf = Array.IndexOf(buffer, '\n', start, count - start);
                    int end = lf < 0 ? count : lf + 1;
                    writer.Write(buffer, start, end - start);
                    if (lf >= 0)
                    {
                        atLineStart = true;
                    }

                    start = end;
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                try
                {
                    writer.Dispose();
                }
                catch (IOException)
                {
                    // Nothing more can be done when the final flush fails.
                }
            }
        }
    }
}
