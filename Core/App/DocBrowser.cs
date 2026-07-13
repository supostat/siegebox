using System;
using System.Collections.Generic;
using Siegebox.Documentation;
using Siegebox.Vfs;

namespace Siegebox.App
{
    /// <summary>
    /// Host-agnostic logic behind the doc browser. Categories are derived from the live command
    /// names grouped by their manual category, so a mod command with no manual entry falls into
    /// "other". Page bodies and per-box hints are read from the VFS under the launching session's
    /// credentials — never ambient root — so page permissions are enforced exactly like the shell.
    /// </summary>
    public sealed class DocBrowser
    {
        private const string ManDirectory = "/usr/share/man/";
        private const string UncategorizedName = "other";

        private readonly Func<IReadOnlyCollection<string>> commandNames;
        private readonly Manual manual;
        private readonly VirtualFileSystem vfs;
        private readonly Credentials credentials;
        private readonly string boxManifestPath;

        public DocBrowser(
            Func<IReadOnlyCollection<string>> commandNames,
            Manual manual,
            VirtualFileSystem vfs,
            Credentials credentials,
            string boxManifestPath)
        {
            this.commandNames = commandNames ?? throw new ArgumentNullException(nameof(commandNames));
            this.manual = manual ?? throw new ArgumentNullException(nameof(manual));
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            this.boxManifestPath = boxManifestPath ?? throw new ArgumentNullException(nameof(boxManifestPath));
        }

        public IReadOnlyList<DocCategory> Categories
        {
            get
            {
                var grouped = new Dictionary<string, List<DocEntry>>(StringComparer.Ordinal);
                foreach (var name in commandNames())
                {
                    var category = manual.TryGet(name, out var page) ? page.Category : UncategorizedName;
                    var description = page is null ? "" : page.Description;
                    if (!grouped.TryGetValue(category, out var entries))
                    {
                        entries = new List<DocEntry>();
                        grouped.Add(category, entries);
                    }

                    entries.Add(new DocEntry(name, description));
                }

                return BuildCategories(grouped);
            }
        }

        /// <summary>
        /// Renders the not-found/error line with <see cref="VfsErrorText"/> so it reads identically
        /// to the <c>man</c> command; this deliberately diverges from FileManagerApp's raw-enum
        /// <c>{path}: {error}</c> format because the doc browser mirrors the shell, not the file view.
        /// </summary>
        public string ReadPage(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            IByteStream stream;
            try
            {
                stream = vfs.Open(ManDirectory + name, OpenMode.Read, credentials);
            }
            catch (VfsException error)
            {
                return $"man: {error.Path}: {VfsErrorText.MessageFor(error.Error)}";
            }

            try
            {
                return ByteStreamText.ReadToEnd(stream);
            }
            finally
            {
                stream.CloseRead();
            }
        }

        public IReadOnlyList<string> ReadHints()
        {
            IByteStream stream;
            try
            {
                stream = vfs.Open(boxManifestPath, OpenMode.Read, credentials);
            }
            catch (VfsException)
            {
                return Array.Empty<string>();
            }

            string json;
            try
            {
                json = ByteStreamText.ReadToEnd(stream);
            }
            finally
            {
                stream.CloseRead();
            }

            return BoxManifest.Parse(json).Hints;
        }

        private static IReadOnlyList<DocCategory> BuildCategories(Dictionary<string, List<DocEntry>> grouped)
        {
            var names = new List<string>(grouped.Keys);
            names.Sort(StringComparer.Ordinal);
            var categories = new List<DocCategory>(names.Count);
            foreach (var name in names)
            {
                var entries = grouped[name];
                entries.Sort((left, right) => string.CompareOrdinal(left.CommandName, right.CommandName));
                categories.Add(new DocCategory(name, entries));
            }

            return categories;
        }
    }
}
