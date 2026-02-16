using HandlebarsDotNet;
using System.Reflection;

namespace Bam.Generators
{
    /// <summary>
    /// Defines a contract for loading and rendering Handlebars templates from assembly embedded resources.
    /// Extends <see cref="ITemplateRenderer"/> with resource management capabilities.
    /// </summary>
    public interface IHandlebarsEmbeddedResources : ITemplateRenderer
    {
        /// <summary>
        /// Gets or sets the assemblies containing embedded Handlebars template resources.
        /// </summary>
        IEnumerable<Assembly> Assemblies { get; set; }

        /// <summary>
        /// Gets a value indicating whether embedded resource templates have been loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets or sets the dictionary of compiled templates keyed by template name.
        /// </summary>
        Dictionary<string, HandlebarsTemplate<object, object>> Templates { get; set; }

        /// <summary>
        /// Reloads all Handlebars templates from the embedded resources of the configured assemblies.
        /// </summary>
        void Reload();
        //string Render(string templateName, object data);
    }
}