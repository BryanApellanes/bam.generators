using System.Reflection;

namespace Bam.Generators
{
    /// <summary>
    /// Bundles Handlebars template directories and embedded resources into a single set
    /// that can be used to render templates or converted to a <see cref="ITemplateRenderer"/>.
    /// </summary>
    public class HandlebarsTemplateSet
    {
        /// <summary>
        /// The default file system path for Handlebars templates.
        /// </summary>
        public const string DefaultPath = "./Handlebars";
        private readonly HandlebarsTemplateRenderer _renderer;

        /// <summary>
        /// Initializes a new instance using embedded resources from the executing assembly and no directory sources.
        /// </summary>
        public HandlebarsTemplateSet()
        {
            HandlebarsDirectories = new HashSet<HandlebarsDirectory>();
            HandlebarsEmbeddedResources = new HandlebarsEmbeddedResources(Assembly.GetExecutingAssembly());
            _renderer = new HandlebarsTemplateRenderer(HandlebarsEmbeddedResources, HandlebarsDirectories.ToArray());
        }

        /// <summary>
        /// Initializes a new instance using the specified directory path and embedded resources from the executing assembly.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing Handlebars template files.</param>
        public HandlebarsTemplateSet(string directoryPath)
        {
            HandlebarsDirectories = new HashSet<HandlebarsDirectory>();
            HandlebarsDirectories.Add(new HandlebarsDirectory(directoryPath));
            HandlebarsEmbeddedResources = new HandlebarsEmbeddedResources(Assembly.GetExecutingAssembly());
            _renderer = new HandlebarsTemplateRenderer(HandlebarsEmbeddedResources, HandlebarsDirectories.ToArray());
        }

        /// <summary>
        /// Initializes a new instance using embedded resources from the specified assembly.
        /// </summary>
        /// <param name="embeddedResourceContainer">The assembly containing embedded Handlebars template resources.</param>
        public HandlebarsTemplateSet(Assembly embeddedResourceContainer)
        {
            HandlebarsDirectories = new HashSet<HandlebarsDirectory>();
            HandlebarsEmbeddedResources = new HandlebarsEmbeddedResources(embeddedResourceContainer);
            _renderer = new HandlebarsTemplateRenderer(HandlebarsEmbeddedResources, HandlebarsDirectories.ToArray());
        }

        /// <summary>
        /// Gets or sets the set of directory-based template sources.
        /// </summary>
        public HashSet<HandlebarsDirectory> HandlebarsDirectories { get; set; }

        /// <summary>
        /// Gets or sets the embedded resource-based template source.
        /// </summary>
        public HandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }

        /// <summary>
        /// Renders the specified template with the given model data.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="modelData">The data model to pass to the template.</param>
        /// <returns>The rendered template output as a string.</returns>
        public string Render(string templateName, object modelData)
        {
            return ToRenderer().Render(templateName, modelData);
        }

        /// <summary>
        /// Creates a new <see cref="ITemplateRenderer"/> from the current directories and embedded resources.
        /// </summary>
        /// <returns>A new <see cref="HandlebarsTemplateRenderer"/> instance.</returns>
        public ITemplateRenderer ToRenderer()
        {
            return new HandlebarsTemplateRenderer(HandlebarsEmbeddedResources, HandlebarsDirectories.ToArray());
        }
    }
}