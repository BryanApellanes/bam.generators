namespace Bam.Generators
{
    /// <summary>
    /// A strongly-typed Handlebars template renderer that renders instances of <typeparamref name="T"/>
    /// using templates named after the type or a specified template name.
    /// </summary>
    /// <typeparam name="T">The type of object this renderer handles.</typeparam>
    public class HandlebarsTemplateRenderer<T> : HandlebarsTemplateRenderer, ITemplateRenderer<T>
    {
        /// <summary>
        /// Renders the specified object using a template named after <typeparamref name="T"/>.
        /// </summary>
        /// <param name="toRender">The object to render. Must not be null.</param>
        /// <returns>The rendered template output as a string.</returns>
        public string Render(T toRender)
        {
            if(toRender == null)
            {
                throw new ArgumentNullException(nameof(toRender));
            }

            return Render(typeof(T).Name, toRender);
        }

        /// <summary>
        /// Renders the specified object using the named template.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="toRender">The object to render.</param>
        /// <returns>The rendered template output as a string.</returns>
        public string Render(string templateName, T? toRender)
        {
            return base.Render(templateName, toRender);
        }

        /// <summary>
        /// Renders the specified object using a template named after <typeparamref name="T"/> and writes the output to a stream.
        /// </summary>
        /// <param name="toRender">The object to render.</param>
        /// <param name="output">The stream to write the rendered output to.</param>
        public void Render(T? toRender, Stream output)
        {
            base.Render(toRender, output);
        }

        /// <summary>
        /// Renders the specified object using the named template and writes the output to a stream.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="toRender">The object to render.</param>
        /// <param name="output">The stream to write the rendered output to.</param>
        public void Render(string templateName, T? toRender, Stream output)
        {
            base.Render(templateName, toRender, output);
        }
    }
}
