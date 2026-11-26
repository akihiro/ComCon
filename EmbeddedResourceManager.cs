namespace ComCon
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Resources;

    /// <summary>
    /// A ResourceManager that reads localized resources embedded in the main assembly
    /// (e.g. "ComCon.Strings.ja.resources") instead of satellite assemblies, so that ComCon ships as a single exe.
    /// Cultures with no embedded resources fall back to the neutral (English) resources as usual.
    /// </summary>
    internal sealed class EmbeddedResourceManager : ResourceManager
    {
        private readonly Assembly assembly;
        private readonly Dictionary<string, ResourceSet> cache = new Dictionary<string, ResourceSet>();

        public EmbeddedResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly)
        {
            this.assembly = assembly;
        }

        /// <summary>
        /// Points the build-generated <see cref="Strings"/> class at this manager. The generated class keeps its
        /// ResourceManager in a private static field named "resourceMan" (stable across resource generators since .NET 2.0).
        /// If the field is not found, messages simply stay in English.
        /// </summary>
        public static void InstallForStrings()
        {
            var field = typeof(Strings).GetField("resourceMan", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null && field.FieldType == typeof(ResourceManager))
            {
                field.SetValue(null, new EmbeddedResourceManager("ComCon.Strings", typeof(Strings).Assembly));
            }
        }

        protected override ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
        {
            // Walk ja-JP -> ja -> (invariant) looking for embedded resources.
            for (var c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
            {
                var set = LoadEmbedded(c.Name);
                if (set != null)
                {
                    return set;
                }

                if (!tryParents)
                {
                    break;
                }
            }

            // Neutral resources (and anything else) are handled by the base implementation.
            return base.InternalGetResourceSet(culture, createIfNotExists, tryParents);
        }

        private ResourceSet LoadEmbedded(string cultureName)
        {
            lock (cache)
            {
                ResourceSet set;
                if (!cache.TryGetValue(cultureName, out set))
                {
                    var stream = assembly.GetManifestResourceStream(BaseNameField + "." + cultureName + ".resources");
                    set = stream == null ? null : new ResourceSet(stream);
                    cache[cultureName] = set;
                }

                return set;
            }
        }
    }
}
