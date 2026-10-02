using Bam.Logging;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Handlebars-based <see cref="IDecoratorCodeWriter"/>. Renders the embedded <c>Decorator</c> template using
    /// the shared <see cref="Bam.Generators"/> Handlebars infrastructure. Mirrors
    /// <c>HandlebarsServiceClientCodeWriter</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A writer built with a directory source lets a template of the same name in that directory override the
    /// embedded one. That is for generating source at build time, where the output is reviewed and compiled by
    /// the developer. A writer from <see cref="EmbeddedOnly"/> has no directory source, which is what anything
    /// that compiles and loads the rendered source at runtime must use: a file on disk never decides what code
    /// the process runs.
    /// </para>
    /// <para>
    /// The template is self-contained — it references no partials — because Handlebars partials are registered
    /// process-wide, where a template loaded by any other generator could replace them.
    /// </para>
    /// </remarks>
    public class HandlebarsDecoratorCodeWriter : Loggable, IDecoratorCodeWriter
    {
        /// <summary>The name of the template that renders a decorator and its extension methods.</summary>
        public const string TemplateName = "Decorator";

        /// <summary>
        /// Initializes a new instance using this assembly's embedded templates plus a <c>./Templates</c> override
        /// directory, resolved against the current directory and created if missing. For build-time generation.
        /// </summary>
        public HandlebarsDecoratorCodeWriter()
            : this(new HandlebarsDirectory("./Templates"), new HandlebarsEmbeddedResources(typeof(HandlebarsDecoratorCodeWriter).Assembly))
        {
        }

        /// <summary>Initializes a new instance with the specified template sources.</summary>
        /// <param name="handlebarsDirectory">The directory-based template source, consulted first; null for none.</param>
        /// <param name="handlebarsEmbeddedResources">The embedded-resource template source, the default.</param>
        public HandlebarsDecoratorCodeWriter(IHandlebarsDirectory? handlebarsDirectory, IHandlebarsEmbeddedResources handlebarsEmbeddedResources)
        {
            ArgumentNullException.ThrowIfNull(handlebarsEmbeddedResources);

            HandlebarsDirectory = handlebarsDirectory;
            HandlebarsEmbeddedResources = handlebarsEmbeddedResources;
        }

        /// <summary>
        /// Creates a writer that renders this assembly's embedded templates and nothing else. It reads no
        /// directory and creates none.
        /// </summary>
        public static HandlebarsDecoratorCodeWriter EmbeddedOnly()
        {
            return new HandlebarsDecoratorCodeWriter(null, new HandlebarsEmbeddedResources(typeof(HandlebarsDecoratorCodeWriter).Assembly));
        }

        /// <summary>Gets or sets a value indicating whether templates have been loaded.</summary>
        protected bool Loaded { get; set; }

        /// <summary>Gets or sets the directory-based template source (override); null when there is none.</summary>
        public IHandlebarsDirectory? HandlebarsDirectory { get; set; }

        /// <summary>Gets or sets the embedded-resource template source (default).</summary>
        public IHandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }

        /// <summary>Loads templates if not already loaded.</summary>
        public void Load()
        {
            if (!Loaded)
            {
                Reload();
            }
        }

        /// <summary>Forces a reload of all template sources.</summary>
        public void Reload()
        {
            HandlebarsDirectory?.Reload();
            HandlebarsEmbeddedResources.Reload();
            Loaded = true;
        }

        /// <inheritdoc />
        public string GetSource(DecoratorModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            Load();
            return Render(TemplateName, model);
        }

        /// <inheritdoc />
        public void WriteDecorator(DecoratorModel model, Stream output)
        {
            string code = GetSource(model);
            StreamWriter writer = new StreamWriter(output, leaveOpen: true);
            writer.Write(code);
            writer.Flush();
        }

        private string Render(string templateName, DecoratorModel model)
        {
            if (HandlebarsDirectory?.Templates?.ContainsKey(templateName) == true)
            {
                return HandlebarsDirectory.Render(templateName, model);
            }
            if (HandlebarsEmbeddedResources.Templates?.ContainsKey(templateName) == true)
            {
                return HandlebarsEmbeddedResources.Render(templateName, model);
            }
            throw new InvalidOperationException($"Decorator template '{templateName}' not found.");
        }
    }
}
