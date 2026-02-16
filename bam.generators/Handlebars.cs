namespace Bam.Generators
{
    /// <summary>
    /// Provides static methods for rendering Handlebars templates, delegating to either a
    /// <see cref="HandlebarsDirectory"/> or <see cref="HandlebarsEmbeddedResources"/> template source.
    /// </summary>
    public static class Handlebars
    {
        /// <summary>
        /// Renders the specified Handlebars template with the given model and returns the result as a string.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="renderModel">The data model to pass to the template.</param>
        /// <returns>The rendered template output as a string.</returns>
        public static string Render(string templateName, object renderModel)
        {
            MemoryStream ms = new MemoryStream();
            Render(templateName, renderModel, ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ReadToEnd();
        }

        /// <summary>
        /// Gets or sets the directory-based template source for loading Handlebars templates from the file system.
        /// </summary>
        public static HandlebarsDirectory HandlebarsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the embedded resource-based template source for loading Handlebars templates from assemblies.
        /// </summary>
        public static HandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }

        /// <summary>
        /// Renders the specified Handlebars template with the given model and writes the output to a stream.
        /// Checks the directory-based templates first, then falls back to embedded resource templates.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="renderModel">The data model to pass to the template.</param>
        /// <param name="output">The stream to write the rendered output to.</param>
        public static void Render(string templateName, object renderModel, Stream output)
        {
            EnsureTemplatesAreLoaded();
            if ((HandlebarsDirectory?.Templates?.ContainsKey(templateName)) == true)
            {
                string code = HandlebarsDirectory.Render(templateName, renderModel);

                code.WriteToStream(output, false);
            }
            else if ((HandlebarsEmbeddedResources?.Templates?.ContainsKey(templateName)) == true)
            {
                string code = HandlebarsEmbeddedResources.Render(templateName, renderModel);
                code.WriteToStream(output, false);
            }
            else
            {
                Args.Throw<InvalidOperationException>("Specified template ('{0}') not found", templateName);
            }
        }

        private static void EnsureTemplatesAreLoaded()
        {
            Args.ThrowIf(HandlebarsDirectory == null && HandlebarsEmbeddedResources == null, "Must specify at least one of Handlebars.HandlebarsDirectory or Handlebars.EmbeddedResources");
            if (HandlebarsDirectory != null && !HandlebarsDirectory.IsLoaded)
            {
                HandlebarsDirectory.Reload();
            }

            if (HandlebarsEmbeddedResources != null && !HandlebarsEmbeddedResources.IsLoaded)
            {
                HandlebarsEmbeddedResources.Reload();
            }
        }
    }
}
