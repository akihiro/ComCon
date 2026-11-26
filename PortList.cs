namespace ComCon
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO.Ports;
    using System.Linq;
    using System.Management;
    using System.Text.RegularExpressions;

    internal static class PortList
    {
        private static readonly Regex ComName = new Regex(@"COM\d+", RegexOptions.IgnoreCase);

        public static void Print()
        {
            // Some drivers append junk characters to the names from GetPortNames(), so keep only the COMn part.
            var names = SerialPort.GetPortNames()
                .Select(n => ComName.Match(n))
                .Where(m => m.Success)
                .Select(m => m.Value.ToUpperInvariant())
                .Distinct()
                .OrderBy(PortNumber)
                .ToList();

            if (names.Count == 0)
            {
                Console.WriteLine(Strings.NoPortsFound);
                return;
            }

            var friendly = GetFriendlyNames();
            foreach (var name in names)
            {
                string description;
                friendly.TryGetValue(name, out description);
                Console.WriteLine("  {0,-7} {1}", name, description ?? string.Empty);
            }
        }

        private static int PortNumber(string name)
        {
            int n;
            int.TryParse(name.Substring(3), NumberStyles.None, CultureInfo.InvariantCulture, out n);
            return n;
        }

        /// <summary>Gets the names shown in Device Manager (for example "USB Serial Device (COM3)") from WMI.</summary>
        private static Dictionary<string, string> GetFriendlyNames()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                using (var objects = searcher.Get())
                {
                    foreach (ManagementBaseObject obj in objects)
                    {
                        var name = obj["Name"] as string;
                        if (name == null)
                        {
                            continue;
                        }

                        var m = Regex.Match(name, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            result[m.Groups[1].Value.ToUpperInvariant()] = name;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Without WMI, the ports are listed without names.
            }

            return result;
        }
    }
}
