namespace ComCon
{
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// Helpers for localized messages. Message text lives in Strings.resx (English, neutral)
    /// and Strings.&lt;culture&gt;.resx; the strongly typed <see cref="Strings"/> class is generated at build time.
    /// </summary>
    internal static class Localization
    {
        public static string Format(string format, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }

        /// <summary>
        /// Applies --lang before full argument parsing, so that even parse errors are shown in the requested language.
        /// An invalid value is ignored here and reported later by <see cref="Options.Parse"/>.
        /// </summary>
        public static void ApplyFromArgs(string[] args)
        {
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--lang")
                {
                    try
                    {
                        Apply(CultureInfo.GetCultureInfo(args[i + 1]));
                    }
                    catch (CultureNotFoundException)
                    {
                    }
                }
            }
        }

        /// <summary>Sets the UI culture for the current thread and for threads created later (the receiver thread).</summary>
        public static void Apply(CultureInfo culture)
        {
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
