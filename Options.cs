namespace ComCon
{
    using System;
    using System.Globalization;
    using System.IO.Ports;
    using System.Text;

    /// <summary>Connection settings parsed from the command line.</summary>
    internal sealed class Options
    {
        public string PortName;
        public int BaudRate = 115200;
        public int DataBits = 8;
        public Parity Parity = Parity.None;
        public StopBits StopBits = StopBits.One;
        public Handshake Handshake = Handshake.None;
        public bool DtrEnable = true;
        public bool RtsEnable = true;
        public bool LocalEcho;
        public string NewLine = "\r";
        public Encoding Encoding = new UTF8Encoding(false);

        public CultureInfo Language;

        public bool ShowHelp;
        public bool ListPorts;
        public bool ShowVersion;

        /// <summary>A short summary of the settings, such as "COM3 115200 8N1".</summary>
        public string Summary
        {
            get
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1} {2}{3}{4}",
                    PortName,
                    BaudRate,
                    DataBits,
                    ParityChar(Parity),
                    StopBitsText(StopBits));
            }
        }

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "-h":
                    case "--help":
                    case "/?":
                        o.ShowHelp = true;
                        break;
                    case "-v":
                    case "--version":
                        o.ShowVersion = true;
                        break;
                    case "-l":
                    case "--list":
                        o.ListPorts = true;
                        break;
                    case "-b":
                    case "--baud":
                        o.BaudRate = ParseInt(a, Next(args, ref i), 1, int.MaxValue);
                        break;
                    case "-d":
                    case "--data":
                        o.DataBits = ParseInt(a, Next(args, ref i), 5, 8);
                        break;
                    case "-p":
                    case "--parity":
                        o.Parity = ParseParity(a, Next(args, ref i));
                        break;
                    case "-s":
                    case "--stop":
                        o.StopBits = ParseStopBits(a, Next(args, ref i));
                        break;
                    case "-f":
                    case "--flow":
                        o.Handshake = ParseHandshake(a, Next(args, ref i));
                        break;
                    case "-e":
                    case "--echo":
                        o.LocalEcho = true;
                        break;
                    case "-n":
                    case "--newline":
                        o.NewLine = ParseNewLine(a, Next(args, ref i));
                        break;
                    case "--encoding":
                        o.Encoding = ParseEncoding(Next(args, ref i));
                        break;
                    case "--lang":
                        o.Language = ParseLanguage(a, Next(args, ref i));
                        break;
                    case "--no-dtr":
                        o.DtrEnable = false;
                        break;
                    case "--no-rts":
                        o.RtsEnable = false;
                        break;
                    default:
                        if (a.StartsWith("-", StringComparison.Ordinal))
                        {
                            throw new ArgumentException(Localization.Format(Strings.UnknownOption, a));
                        }

                        if (o.PortName != null)
                        {
                            throw new ArgumentException(Localization.Format(Strings.MultiplePorts, o.PortName, a));
                        }

                        o.PortName = NormalizePortName(a);
                        break;
                }
            }

            return o;
        }

        private static CultureInfo ParseLanguage(string name, string s)
        {
            try
            {
                return CultureInfo.GetCultureInfo(s);
            }
            catch (CultureNotFoundException)
            {
                throw new ArgumentException(Localization.Format(Strings.InvalidValue, name, s));
            }
        }

        private static string Format(string format, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException(Localization.Format(Strings.MissingValue, args[i]));
            }

            i++;
            return args[i];
        }

        private static int ParseInt(string name, string value, int min, int max)
        {
            int n;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < min || n > max)
            {
                throw new ArgumentException(Localization.Format(Strings.InvalidValue, name, value));
            }

            return n;
        }

        private static string NormalizePortName(string s)
        {
            int n;
            if (int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out n))
            {
                return "COM" + n.ToString(CultureInfo.InvariantCulture);
            }

            return s.ToUpperInvariant();
        }

        private static Parity ParseParity(string name, string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "none": case "n": return Parity.None;
                case "odd": case "o": return Parity.Odd;
                case "even": case "e": return Parity.Even;
                case "mark": case "m": return Parity.Mark;
                case "space": case "s": return Parity.Space;
                default: throw new ArgumentException(Localization.Format(Strings.InvalidValue, name, s));
            }
        }

        private static StopBits ParseStopBits(string name, string s)
        {
            switch (s)
            {
                case "1": return StopBits.One;
                case "1.5": return StopBits.OnePointFive;
                case "2": return StopBits.Two;
                default: throw new ArgumentException(Localization.Format(Strings.InvalidValue, name, s));
            }
        }

        private static Handshake ParseHandshake(string name, string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "none": return Handshake.None;
                case "rtscts": case "hw": case "hardware": return Handshake.RequestToSend;
                case "xonxoff": case "sw": case "software": return Handshake.XOnXOff;
                case "both": return Handshake.RequestToSendXOnXOff;
                default: throw new ArgumentException(Localization.Format(Strings.InvalidValue, name, s));
            }
        }

        private static string ParseNewLine(string name, string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "cr": return "\r";
                case "lf": return "\n";
                case "crlf": return "\r\n";
                default: throw new ArgumentException(Localization.Format(Strings.InvalidValue, name, s));
            }
        }

        private static Encoding ParseEncoding(string s)
        {
            if (string.Equals(s, "utf-8", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s, "utf8", StringComparison.OrdinalIgnoreCase))
            {
                return new UTF8Encoding(false);
            }

            try
            {
                return Encoding.GetEncoding(s);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException(Localization.Format(Strings.UnknownEncoding, s));
            }
        }

        private static char ParityChar(Parity p)
        {
            switch (p)
            {
                case Parity.Odd: return 'O';
                case Parity.Even: return 'E';
                case Parity.Mark: return 'M';
                case Parity.Space: return 'S';
                default: return 'N';
            }
        }

        private static string StopBitsText(StopBits s)
        {
            switch (s)
            {
                case StopBits.OnePointFive: return "1.5";
                case StopBits.Two: return "2";
                default: return "1";
            }
        }
    }
}
