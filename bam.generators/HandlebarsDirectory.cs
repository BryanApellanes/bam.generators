using HandlebarsDotNet;
using Bam.Logging;

namespace Bam.Generators
{
    /// <summary>
    /// Loads, compiles, and manages Handlebars templates from a file system directory,
    /// including support for partial templates from subdirectories.
    /// </summary>
    public class HandlebarsDirectory : IHandlebarsDirectory
    {
        /// <summary>
        /// Implicitly converts a <see cref="HandlebarsDirectory"/> to a <see cref="DirectoryInfo"/>.
        /// </summary>
        /// <param name="dir">The <see cref="HandlebarsDirectory"/> to convert.</param>
        public static implicit operator DirectoryInfo(HandlebarsDirectory dir)
        {
            return dir.Directory;
        }

        /// <summary>
        /// Initializes a new instance that loads templates from the specified directory.
        /// </summary>
        /// <param name="directory">The directory containing Handlebars template files.</param>
        /// <param name="logger">An optional logger; defaults to <see cref="Log.Default"/> if not provided.</param>
        public HandlebarsDirectory(DirectoryInfo directory, ILogger? logger = null)
        {
            Args.ThrowIfNull(directory, "directory");
            FileExtension = "hbs";
            Directory = directory;
            Logger = logger ?? Log.Default!;
            if (!directory.Exists)
            {
                Logger.Warning("Handlebars directory does not exist: {0}", _directory!.FullName);
            }
        }

        /// <summary>
        /// Initializes a new instance that loads templates from the specified directory path.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing Handlebars template files.</param>
        /// <param name="logger">An optional logger; defaults to <see cref="Log.Default"/> if not provided.</param>
        public HandlebarsDirectory(string directoryPath, ILogger? logger = null) : this(new DirectoryInfo(directoryPath), logger)
        {
        }

        /// <summary>
        /// Gets the logger used for diagnostics.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the dictionary of compiled template functions keyed by template name.
        /// </summary>
        public Dictionary<string, Func<object, string>> Templates { get; private set; } = null!;

        /// <summary>
        /// Determines whether a template with the specified name has been loaded.
        /// </summary>
        /// <param name="templateName">The name of the template to check for.</param>
        /// <returns>True if the template exists; otherwise, false.</returns>
        public bool HasTemplate(string templateName)
        {
            return Templates.ContainsKey(templateName);
        }

        /// <summary>
        /// Creates a new <see cref="HandlebarsDirectory"/> that combines this instance's templates and partials
        /// with those from the specified directories.
        /// </summary>
        /// <param name="dirs">Additional directories whose templates and partials should be merged.</param>
        /// <returns>A new <see cref="HandlebarsDirectory"/> containing the merged templates.</returns>
        public HandlebarsDirectory CombineWith(params HandlebarsDirectory[] dirs)
        {
            Reload();
            HandlebarsDirectory combined = new HandlebarsDirectory(Directory);
            combined.CopyProperties(this);
            foreach (HandlebarsDirectory dir in dirs)
            {
                dir.Reload();
                foreach (DirectoryInfo partialDir in dir.PartialsDirectories)
                {
                    combined.PartialsDirectories.Add(partialDir);
                }
                foreach (string key in dir.Templates.Keys)
                {
                    combined.Templates.TryAdd(key, dir.Templates[key]);
                }
            }
            combined.Reload();
            return combined;
        }

        /// <summary>
        /// Adds a template by writing its source to a file in the directory and optionally reloading all templates.
        /// </summary>
        /// <param name="templateName">The name for the template (used as the file name without extension).</param>
        /// <param name="source">The Handlebars template source text.</param>
        /// <param name="reload">If true, reloads all templates from disk after adding; otherwise, compiles in memory only.</param>
        public void AddTemplate(string templateName, string source, bool reload = false)
        {
            string filePath = Path.Combine(Directory.FullName, $"{templateName}.{FileExtension}");
            source.SafeWriteToFile(filePath, true);
            if (reload)
            {
                Reload();
            }
            else
            {
                Templates.TryAdd(templateName, (obj) =>
                {
                    HandlebarsTemplate<object, object> template = HandlebarsDotNet.Handlebars.Compile(source);
                    return (template.DynamicInvoke(obj, obj) as string)!;
                });
            }
        }

        /// <summary>
        /// Adds a partial template by writing its source to a file and registering it with the Handlebars engine.
        /// </summary>
        /// <param name="templateName">The name for the partial template.</param>
        /// <param name="source">The Handlebars partial template source text.</param>
        /// <param name="reload">If true, reloads all templates from disk after adding; otherwise, registers in memory only.</param>
        public void AddPartial(string templateName, string source, bool reload = false)
        {
            if (PartialsDirectories == null)
            {
                AddPartialsDirectory(Path.Combine(Directory.FullName, "Partials"));
            }
            string filePath = Path.Combine(Directory.FullName, $"{templateName}.{FileExtension}");
            source.SafeWriteToFile(filePath, true);
            if (reload)
            {
                Reload();
            }
            else
            {
                HandlebarsDotNet.Handlebars.RegisterTemplate(templateName, source);
            }
        }

        /// <summary>
        /// Renders the specified template with the given data model, loading templates first if needed.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="data">The data model to pass to the template.</param>
        /// <returns>The rendered output string, or an empty string if the template is not found.</returns>
        public string Render(string templateName, object data)
        {
            if (!_loaded)
            {
                Reload();
            }
            if (Templates.ContainsKey(templateName))
            {
                return Templates[templateName](data);
            }
            return string.Empty;
        }

        DirectoryInfo _directory = null!;
        /// <summary>
        /// Gets or sets the directory from which templates are loaded. Setting this also initializes partials subdirectories.
        /// </summary>
        public DirectoryInfo Directory
        {
            get => _directory;
            set => SetDirectory(value);
        }

        /// <summary>
        /// Adds a directory to the set of partials directories and triggers a reload of all templates.
        /// </summary>
        /// <param name="partialsDirectory">The path to the directory containing partial template files.</param>
        public void AddPartialsDirectory(string partialsDirectory)
        {
            if (PartialsDirectories == null)
            {
                PartialsDirectories = new HashSet<DirectoryInfo>
                {
                    new DirectoryInfo(partialsDirectory)
                };
            }
            else
            {
                PartialsDirectories.Add(new DirectoryInfo(partialsDirectory));
            }
            Reload();
        }

        /// <summary>
        /// Gets or sets the file extension used when scanning for template files (default is "hbs").
        /// </summary>
        public string FileExtension { get; set; }

        /// <summary>
        /// Gets or sets the set of directories that contain partial templates.
        /// </summary>
        public HashSet<DirectoryInfo> PartialsDirectories { get; set; } = null!;
        readonly object _reloadLock = new object();
        bool _loaded = false;

        /// <summary>
        /// Gets a value indicating whether templates have been loaded from disk.
        /// </summary>
        public bool IsLoaded => _loaded;

        /// <summary>
        /// Forces a full reload of all templates and partials from disk.
        /// </summary>
        public void Reload()
        {
            Load(true);
        }

        /// <summary>
        /// Loads all templates from the directory and partials directories. Registers partials with the
        /// Handlebars engine and compiles main templates into callable functions.
        /// </summary>
        /// <param name="reload">If true, reloads even if templates have already been loaded.</param>
        public void Load(bool reload)
        {
            if (!_loaded || reload)
            {
                lock (_reloadLock)
                {
                    Templates = new Dictionary<string, Func<object, string>>();
                    if (PartialsDirectories != null)
                    {
                        foreach (DirectoryInfo partialsDirectory in PartialsDirectories)
                        {
                            if (partialsDirectory.Exists)
                            {
                                foreach (FileInfo partial in partialsDirectory.GetFiles($"*.{FileExtension}"))
                                {
                                    string shortName = Path.GetFileNameWithoutExtension(partial.FullName);
                                    string longName = partial.FullName.Truncate($".{FileExtension}".Length);
                                    string content = partial.ReadAllText();
                                    HandlebarsDotNet.Handlebars.RegisterTemplate(shortName, content);
                                    HandlebarsDotNet.Handlebars.RegisterTemplate(longName, content);
                                }
                            }
                        }
                    }
                    if (Directory != null && Directory.Exists)
                    {
                        foreach (FileInfo file in Directory!.GetFiles($"*.{FileExtension}"))
                        {
                            AddCompiledTemplateFile(file);
                        }
                    }
                    _loaded = true;
                }
            }
        }

        /// <summary>
        /// Compiles a Handlebars template file and adds it to the <see cref="Templates"/> dictionary
        /// under both its short name and full path-based name.
        /// </summary>
        /// <param name="file">The template file to compile and add.</param>
        public void AddCompiledTemplateFile(FileInfo file)
        {
            string shortName = Path.GetFileNameWithoutExtension(file.FullName);
            string longName = file.FullName.Truncate($".{FileExtension}".Length);
            string content = file.ReadAllText();
            Func<object, string> func = (obj) =>
            {
                HandlebarsTemplate<object, object> template = HandlebarsDotNet.Handlebars.Compile(content);
                return (template.DynamicInvoke(obj, obj) as string)!;
            };
            Templates.TryAdd(shortName, func);
            Templates.TryAdd(longName, func);
        }

        private void SetDirectory(DirectoryInfo directory)
        {
            _directory = directory;
            if (PartialsDirectories == null)
            {
                AddPartialsDirectory(directory.FullName);
                if (!_directory.Exists)
                {
                    _directory.Create();
                }
                DirectoryInfo? partials = _directory.GetDirectories("Partials").FirstOrDefault();
                if (partials != null)
                {
                    AddPartialsDirectory(partials.FullName);
                }
            }
            Reload();
        }
    }
}
