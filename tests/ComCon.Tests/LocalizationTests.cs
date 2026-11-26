namespace ComCon.Tests
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Resources;
    using System.Text.RegularExpressions;
    using Xunit;

    public class LocalizationTests
    {
        private const string Prefix = "ComCon.Strings.";
        private const string Suffix = ".resources";

        /// <summary>Every culture whose resources are embedded in ComCon.exe (e.g. "ja").</summary>
        public static IEnumerable<object[]> Cultures()
        {
            return typeof(Strings).Assembly.GetManifestResourceNames()
                .Where(n => n.Length > Prefix.Length + Suffix.Length && n.StartsWith(Prefix) && n.EndsWith(Suffix))
                .Select(n => n.Substring(Prefix.Length, n.Length - Prefix.Length - Suffix.Length))
                .Select(c => new object[] { c });
        }

        [Fact]
        public void JapaneseResourcesAreEmbedded()
        {
            Assert.Contains(Cultures(), c => (string)c[0] == "ja");
        }

        [Fact]
        public void NoSatelliteAssemblies()
        {
            // Translations must be embedded in ComCon.exe, not shipped as <culture>\ComCon.resources.dll.
            string dir = Path.GetDirectoryName(typeof(Strings).Assembly.Location);
            Assert.False(File.Exists(Path.Combine(dir, "ja", "ComCon.resources.dll")));
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void EveryCultureHasTheSameKeysAndPlaceholders(string culture)
        {
            Dictionary<string, string> neutral = Load("ComCon.Strings.resources");
            Dictionary<string, string> localized = Load(Prefix + culture + Suffix);

            Assert.Equal(neutral.Keys.OrderBy(k => k), localized.Keys.OrderBy(k => k));
            foreach (string key in neutral.Keys)
            {
                Assert.True(
                    Placeholders(neutral[key]).SetEquals(Placeholders(localized[key])),
                    string.Format("Placeholders differ for '{0}' in culture '{1}'.", key, culture));
            }
        }

        [Theory]
        [InlineData("ja-JP")]
        [InlineData("ja")]
        public void JapaneseCulturesUseJapaneseResources(string culture)
        {
            var manager = new EmbeddedResourceManager("ComCon.Strings", typeof(Strings).Assembly);

            Assert.NotEqual(
                manager.GetString("UnknownOption", CultureInfo.InvariantCulture),
                manager.GetString("UnknownOption", CultureInfo.GetCultureInfo(culture)));
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        public void OtherCulturesFallBackToEnglish(string culture)
        {
            var manager = new EmbeddedResourceManager("ComCon.Strings", typeof(Strings).Assembly);

            Assert.Equal(
                manager.GetString("UnknownOption", CultureInfo.InvariantCulture),
                manager.GetString("UnknownOption", CultureInfo.GetCultureInfo(culture)));
        }

        private static Dictionary<string, string> Load(string resourceName)
        {
            using (Stream stream = typeof(Strings).Assembly.GetManifestResourceStream(resourceName))
            {
                Assert.NotNull(stream);
                using (var reader = new ResourceReader(stream))
                {
                    return reader.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value);
                }
            }
        }

        private static HashSet<string> Placeholders(string s)
        {
            return new HashSet<string>(Regex.Matches(s, @"\{\d+\}").Cast<Match>().Select(m => m.Value));
        }
    }
}
