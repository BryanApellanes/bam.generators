using Bam.Logging;

namespace Bam.Generators
{
    /// <summary>
    /// Renders Handlebars templates by looking up templates from multiple directory sources
    /// and falling back to embedded resource templates. Implements <see cref="ITemplateRenderer"/>.
    /// </summary>
    public class HandlebarsTemplateRenderer : ITemplateRenderer
    {
        /// <summary>
        /// Initializes a new instance using templates from the specified directory path and the executing assembly's embedded resources.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing Handlebars template files.</param>
        public HandlebarsTemplateRenderer(string directoryPath = ".") : this(
            new HandlebarsEmbeddedResources(typeof(HandlebarsTemplateRenderer).Assembly),
            new HandlebarsDirectory(directoryPath))
        {
        }

        /// <summary>
        /// Initializes a new instance using the specified embedded resources and a single directory source.
        /// </summary>
        /// <param name="handlebarsEmbeddedResources">The embedded resource template source.</param>
        /// <param name="handlebarsDirectory">The directory-based template source.</param>
        public HandlebarsTemplateRenderer(HandlebarsEmbeddedResources handlebarsEmbeddedResources, HandlebarsDirectory handlebarsDirectory)
            : this(handlebarsEmbeddedResources, new HandlebarsDirectory[] {handlebarsDirectory})
        {
        }

        /// <summary>
        /// Initializes a new instance using the specified embedded resources and multiple directory sources.
        /// All sources are reloaded upon construction.
        /// </summary>
        /// <param name="handlebarsEmbeddedResources">The embedded resource template source.</param>
        /// <param name="handlebarsDirectories">One or more directory-based template sources.</param>
        public HandlebarsTemplateRenderer(HandlebarsEmbeddedResources handlebarsEmbeddedResources, params HandlebarsDirectory[] handlebarsDirectories)
        {
            HandlebarsDirectories = new HashSet<HandlebarsDirectory>();
            HandlebarsEmbeddedResources = handlebarsEmbeddedResources;
            
            HandlebarsEmbeddedResources.Reload();
            
            foreach (HandlebarsDirectory handlebarsDirectory in handlebarsDirectories)
            {
                handlebarsDirectory.Reload();
                HandlebarsDirectories.Add(handlebarsDirectory);
            }
        }

        /// <summary>
        /// Gets or sets the logger used for diagnostics.
        /// </summary>
        public ILogger Logger { get; set; } = null!;

        /// <summary>
        /// Gets or sets the set of directory-based template sources.
        /// </summary>
        public HashSet<HandlebarsDirectory> HandlebarsDirectories { get; set; }

        /// <summary>
        /// Gets or sets the embedded resource-based template source.
        /// </summary>
        public HandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }

        /// <summary>
        /// Adds a directory-based template source from the specified <see cref="DirectoryInfo"/>.
        /// </summary>
        /// <param name="directoryInfo">The directory to add as a template source.</param>
        public void AddDirectory(DirectoryInfo directoryInfo)
        {
            HandlebarsDirectories.Add(new HandlebarsDirectory(directoryInfo, Logger));
        }

        /// <summary>
        /// Renders the object using a template named after the object's type.
        /// </summary>
        /// <param name="toRender">The object to render. Must not be null.</param>
        /// <returns>The rendered template output as a string.</returns>
        public string Render(object? toRender)
        {
            if(toRender == null)
            {
                throw new ArgumentNullException(nameof(toRender));
            }

            return Render(toRender.GetType().Name, toRender);
        }

        /// <summary>
        /// Renders the specified template with the given data model and returns the result as a string.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="data">The data model to pass to the template.</param>
        /// <returns>The rendered template output as a string.</returns>
        public string Render(string templateName, object? data)
        {            
            MemoryStream ms = new MemoryStream();
            try
            {
                Render(templateName, data, ms, false);
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ReadToEnd();
            }
            finally
            {
                ms.Dispose();
            }
        }
        
        /// <summary>
        /// Renders the object using a template named after the object's type and writes the output to a stream.
        /// </summary>
        /// <param name="toRender">The object to render. Must not be null.</param>
        /// <param name="output">The stream to write the rendered output to.</param>
        public void Render(object? toRender, Stream output)
        {
            Args.ThrowIfNull(toRender, "toRender");

#pragma warning disable CS8602 // Dereference of a possibly null reference.  Previous line will throw if toRender is null
            Render(toRender.GetType().Name, toRender, output);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        /// <summary>
        /// Renders the specified template with the given model and writes the output to a stream without disposing it.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="renderModel">The data model to pass to the template.</param>
        /// <param name="output">The stream to write the rendered output to.</param>
        public void Render(string templateName, object? renderModel, Stream output)
        {
            Render(templateName, renderModel, output, false);
        }

        /// <summary>
        /// Renders the specified template with the given model and writes the output to a stream.
        /// Searches directory-based templates first, then falls back to embedded resource templates.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="renderModel">The data model to pass to the template. If null, no output is written.</param>
        /// <param name="output">The stream to write the rendered output to.</param>
        /// <param name="dispose">If true, disposes the output stream after writing.</param>
        public void Render(string templateName, object? renderModel, Stream output, bool dispose = false)
        {
            if(renderModel == null)
            {
                return;
            }

            if(HandlebarsEmbeddedResources != null && HandlebarsEmbeddedResources.Templates.Count == 0)
            {
                HandlebarsEmbeddedResources.Reload();
            }

            HandlebarsDirectory? handlebarsDirectory = GetHandlebarsDirectory(templateName);
            if (handlebarsDirectory != null)
            { 
                string code = handlebarsDirectory.Render(templateName, renderModel);
                code.WriteToStream(output, dispose);
            }
            else if ((HandlebarsEmbeddedResources?.Templates?.ContainsKey(templateName)) == true)
            {
                string code = HandlebarsEmbeddedResources.Render(templateName, renderModel);
                code.WriteToStream(output, dispose);
            }
            else
            {
                Args.Throw<InvalidOperationException>("Specified template '{0}' not found", templateName);
            }
        }

        private HandlebarsDirectory? GetHandlebarsDirectory(string templateName)
        {
            HandlebarsDirectory? toUse = HandlebarsDirectories.FirstOrDefault(h => h.HasTemplate(templateName));
            if (HandlebarsDirectories.Count(h => h.HasTemplate(templateName)) > 1)
            {
                (Logger ?? Log.Default!).Info("Multiple templates named {0} were found, using {1}", templateName, Path.Combine(toUse!.Directory.FullName, templateName));
            }

            return toUse;
        }
    }
}
