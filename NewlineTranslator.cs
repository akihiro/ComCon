namespace ComCon
{
    /// <summary>How received line endings are translated before they are shown (--recv-newline).</summary>
    internal enum ReceiveNewline
    {
        /// <summary>Show received data unchanged.</summary>
        None,

        /// <summary>The device ends lines with LF only: show a bare LF as CR LF.</summary>
        Lf,

        /// <summary>The device ends lines with CR only: show CR as CR LF.</summary>
        Cr,
    }

    /// <summary>
    /// Translates received line endings for display. The console runs with DISABLE_NEWLINE_AUTO_RETURN,
    /// so a bare LF only moves the cursor down; devices that send LF-only line endings would otherwise
    /// produce "staircase" output. The translator keeps state across calls, so a CR LF pair split between
    /// two reads is not doubled.
    /// </summary>
    internal sealed class NewlineTranslator
    {
        private readonly ReceiveNewline mode;
        private bool lastWasCr;
        private bool skipLf;

        public NewlineTranslator(ReceiveNewline mode)
        {
            this.mode = mode;
        }

        public ReceiveNewline Mode
        {
            get { return mode; }
        }

        /// <summary>The output buffer size needed for <paramref name="inputLength"/> input characters.</summary>
        public static int MaxOutputLength(int inputLength)
        {
            return inputLength * 2;
        }

        /// <summary>
        /// Translates <paramref name="count"/> characters from <paramref name="input"/> into <paramref name="output"/>,
        /// which must hold at least <see cref="MaxOutputLength"/> characters. Returns the number of characters written.
        /// </summary>
        public int Translate(char[] input, int count, char[] output)
        {
            int o = 0;
            for (int i = 0; i < count; i++)
            {
                char c = input[i];
                switch (mode)
                {
                    case ReceiveNewline.Lf:
                        if (c == '\n' && !lastWasCr)
                        {
                            output[o++] = '\r';
                        }

                        output[o++] = c;
                        lastWasCr = c == '\r';
                        break;

                    case ReceiveNewline.Cr:
                        if (skipLf && c == '\n')
                        {
                            // The LF of a CR LF pair: the CR already produced CR LF.
                            skipLf = false;
                            break;
                        }

                        skipLf = false;
                        output[o++] = c;
                        if (c == '\r')
                        {
                            output[o++] = '\n';
                            skipLf = true;
                        }

                        break;

                    default:
                        output[o++] = c;
                        break;
                }
            }

            return o;
        }
    }
}
