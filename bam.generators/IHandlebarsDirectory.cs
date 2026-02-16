using Bam.Logging;

namespace Bam.Generators
{
    /// <summary>
    /// Defines a contract for loading, managing, and rendering Handlebars templates from a directory,
    /// including support for partial templates.
    /// </summary>
    public interface IHandlebarsDirectory
    {
        /// <summary>
        /// Gets or sets the directory containing the Handlebars template files.
        /// </summary>
        DirectoryInfo Directory { get; set; }

        /// <summary>
        /// Gets or sets the file extension used when scanning for template files (e.g., "hbs").
        /// </summary>
        string FileExtension { get; set; }

        /// <summary>
        /// Gets a value indicating whether templates have been loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets the logger used for diagnostics.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Gets or sets the set of directories containing partial templates.
        /// </summary>
        HashSet<DirectoryInfo> PartialsDirectories { get; set; }

        /// <summary>
        /// Gets the dictionary of compiled template functions keyed by template name.
        /// </summary>
        Dictionary<string, Func<object, string>> Templates { get; }

        /// <summary>
        /// Compiles a template file and adds it to the templates dictionary.
        /// </summary>
        /// <param name="file">The template file to compile and add.</param>
        void AddCompiledTemplateFile(FileInfo file);

        /// <summary>
        /// Adds a partial template by name and source text.
        /// </summary>
        /// <param name="templateName">The name of the partial template.</param>
        /// <param name="source">The Handlebars partial template source text.</param>
        /// <param name="reload">If true, reloads all templates after adding.</param>
        void AddPartial(string templateName, string source, bool reload = false);

        /// <summary>
        /// Adds a directory to the set of partials directories.
        /// </summary>
        /// <param name="partialsDirectory">The path to the partials directory.</param>
        void AddPartialsDirectory(string partialsDirectory);

        /// <summary>
        /// Adds a template by name and source text.
        /// </summary>
        /// <param name="templateName">The name of the template.</param>
        /// <param name="source">The Handlebars template source text.</param>
        /// <param name="reload">If true, reloads all templates after adding.</param>
        void AddTemplate(string templateName, string source, bool reload = false);

        /// <summary>
        /// Creates a new <see cref="HandlebarsDirectory"/> that combines this instance's templates with those from the specified directories.
        /// </summary>
        /// <param name="dirs">Additional directories whose templates should be merged.</param>
        /// <returns>A new <see cref="HandlebarsDirectory"/> containing the merged templates.</returns>
        HandlebarsDirectory CombineWith(params HandlebarsDirectory[] dirs);

        /// <summary>
        /// Determines whether a template with the specified name has been loaded.
        /// </summary>
        /// <param name="templateName">The name of the template to check for.</param>
        /// <returns>True if the template exists; otherwise, false.</returns>
        bool HasTemplate(string templateName);

        /// <summary>
        /// Loads all templates, optionally forcing a reload even if already loaded.
        /// </summary>
        /// <param name="reload">If true, forces a reload of all templates.</param>
        void Load(bool reload);

        /// <summary>
        /// Forces a full reload of all templates and partials from disk.
        /// </summary>
        void Reload();

        /// <summary>
        /// Renders the specified template with the given data model.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="data">The data model to pass to the template.</param>
        /// <returns>The rendered template output as a string.</returns>
        string Render(string templateName, object data);
    }
}