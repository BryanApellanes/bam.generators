using Bam.Logging;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Handlebars-based <see cref="IDecoratorCodeWriter"/>. Renders embedded <c>.hbs</c> templates using the
    /// shared <see cref="Bam.Generators"/> Handlebars infrastructure; a template of the same name in the
    /// override directory takes precedence. Mirrors <c>HandlebarsServiceClientCodeWriter</c>.
    /// </summary>
    public class HandlebarsDecoratorCodeWriter : Loggable, IDecoratorCodeWriter
    {
        /// <summary>The name of the template that renders a decorator and its extension methods.</summary>
        public const string TemplateName = "Decorator";

        /// <summary>Initializes a new instance using this assembly's embedded templates plus a <c>./Templates</c> override directory.</summary>
        public HandlebarsDecoratorCodeWriter()
            : this(new HandlebarsDirectory("./Templates"), new HandlebarsEmbeddedResources(typeof(HandlebarsDecoratorCodeWriter).Assembly))
        {
        }

        /// <summary>Initializes a new instance with the specified template sources.</summary>
        /// <param name="handlebarsDirectory">The directory-based template source, consulted first.</param>
        /// <param name="handlebarsEmbeddedResources">The embedded-resource template source, the default.</param>
        public HandlebarsDecoratorCodeWriter(IHandlebarsDirectory handlebarsDirectory, IHandlebarsEmbeddedResources handlebarsEmbeddedResources)
        {
            HandlebarsDirectory = handlebarsDirectory;
            HandlebarsEmbeddedResources = handlebarsEmbeddedResources;
        }

        /// <summary>Gets or sets a value indicating whether templates have been loaded.</summary>
        protected bool Loaded { get; set; }

        /// <summary>Gets or sets the directory-based template source (override).</summary>
        public IHandlebarsDirectory HandlebarsDirectory { get; set; }

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
            HandlebarsDirectory.Reload();
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
            if (HandlebarsEmbeddedResources?.Templates?.ContainsKey(templateName) == true)
            {
                return HandlebarsEmbeddedResources.Render(templateName, model);
            }
            throw new InvalidOperationException($"Decorator template '{templateName}' not found.");
        }
    }
}
