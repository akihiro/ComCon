namespace ComCon
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            EmbeddedResourceManager.InstallForStrings();

            Encoding originalOutputEncoding = null;
            try
            {
                // Show non-ASCII (e.g. Japanese) messages correctly whatever the console code page is. The original encoding is restored on exit.
                originalOutputEncoding = Console.OutputEncoding;
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (IOException)
            {
                originalOutputEncoding = null;
            }

            try
            {
                return Run(args);
            }
            finally
            {
                if (originalOutputEncoding != null)
                {
                    try
                    {
                        Console.OutputEncoding = originalOutputEncoding;
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        private static int Run(string[] args)
        {
            Localization.ApplyFromArgs(args);

            Options options;
            try
            {
                options = Options.Parse(args);
                if (options.Language != null)
                {
                    Localization.Apply(options.Language);
                }
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(Strings.Usage);
                return 1;
            }

            if (options.ShowHelp)
            {
                Console.WriteLine(Strings.Usage);
                return 0;
            }

            if (options.ShowVersion)
            {
                Console.WriteLine("ComCon " + GetVersion());
                return 0;
            }

            if (options.ListPorts)
            {
                PortList.Print();
                return 0;
            }

            if (options.PortName == null)
            {
                Console.Error.WriteLine(Strings.Usage);
                Console.Error.WriteLine();
                Console.Error.WriteLine(Strings.AvailablePorts);
                PortList.Print();
                return 1;
            }

            try
            {
                return new Session(options).Run();
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine(Localization.Format(Strings.PortInUse, options.PortName));
                return 2;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(Localization.Format(Strings.CannotOpenPort, options.PortName, ex.Message));
                return 2;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(Localization.Format(Strings.CannotOpenPort, options.PortName, ex.Message));
                return 2;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        /// <summary>The version set at build time, such as "1.2.3+&lt;commit&gt;" (from the csproj Version or the release tag).</summary>
        private static string GetVersion()
        {
            Assembly assembly = typeof(Program).Assembly;
            var informational = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                assembly, typeof(AssemblyInformationalVersionAttribute));
            return informational != null ? informational.InformationalVersion : assembly.GetName().Version.ToString();
        }
    }
}
