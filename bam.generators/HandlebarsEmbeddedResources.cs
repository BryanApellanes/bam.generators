using System.Reflection;
using System.Text;
using HandlebarsDotNet;

namespace Bam.Generators
{
    /// <summary>
    /// Loads and renders Handlebars templates from assembly embedded resources (files with .hbs extension).
    /// Templates are registered both by their short name and fully qualified resource name.
    /// </summary>
    public class HandlebarsEmbeddedResources : IHandlebarsEmbeddedResources
    {
        /// <summary>
        /// Initializes a new instance that loads templates from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">One or more assemblies containing embedded Handlebars template resources.</param>
        public HandlebarsEmbeddedResources(params Assembly[] assemblies)
        {
            Assemblies = assemblies;
            Templates = new Dictionary<string, HandlebarsTemplate<object, object>>();
        }

        /// <summary>
        /// Initializes a new instance that loads templates from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">A collection of assemblies containing embedded Handlebars template resources.</param>
        public HandlebarsEmbeddedResources(IEnumerable<Assembly> assemblies)
        {
            Assemblies = assemblies;
            Templates = new Dictionary<string, HandlebarsTemplate<object, object>>();
        }
        
        /// <summary>
        /// Gets or sets the assemblies containing embedded Handlebars template resources.
        /// </summary>
        public IEnumerable<Assembly> Assemblies { get; set; }

        /// <summary>
        /// Gets or sets the dictionary of compiled templates keyed by template name.
        /// </summary>
        public Dictionary<string, HandlebarsTemplate<object, object>> Templates
        {
            get;
            set;
        }

        readonly object _reloadLock = new object();
        bool _loaded = false;
        /// <summary>
        /// Gets a value indicating whether embedded resource templates have been loaded.
        /// </summary>
        public bool IsLoaded => _loaded;

        /// <summary>
        /// Renders the specified object using a template named after its type and writes the output to a stream.
        /// </summary>
        /// <param name="toRender">The object to render; its type name is used as the template name.</param>
        /// <param name="output">The stream to write the rendered output to.</param>
        public void Render(object? toRender, Stream output)
        {
            string templateName = toRender?.GetType().Name ?? "default";
            Render(templateName, toRender, output);
        }

        /// <summary>
        /// Renders the specified template with the given object and writes the output to a stream.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="toRender">The data model to pass to the template.</param>
        /// <param name="output">The stream to write the rendered output to.</param>
        public void Render(string templateName, object? toRender, Stream output)
        {
            string rendered = Render(templateName, toRender);

            using (StreamWriter sw = new StreamWriter(output, Encoding.UTF8, Encoding.UTF8.GetByteCount(rendered), true))
            {
                sw.Write(rendered);
                sw.Flush();
            }
        }

        /// <summary>
        /// Renders the specified object using a template named after its type.
        /// </summary>
        /// <param name="toRender">The object to render; its type name is used as the template name.</param>
        /// <returns>The rendered template output as a string.</returns>
        public string Render(object toRender)
        {
            string templateName = toRender?.GetType().Name ?? "default";
           return Render(templateName, toRender);
        }

        /// <summary>
        /// Renders the specified template with the given data model. Loads templates on first call if needed.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="data">The data model to pass to the template.</param>
        /// <returns>The rendered template output as a string.</returns>
        public string Render(string templateName, object? data)
        {
            if (!_loaded)
            {
                Reload();
            }

            if (string.IsNullOrEmpty(templateName))
            {
                throw new ArgumentNullException("templateName");
            }

            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (!Templates.ContainsKey(templateName))
            {
                Args.Throw<InvalidOperationException>("Specified template not found: {0}", templateName);
            }
            return Templates[templateName](data);
        }

        /// <summary>
        /// Reloads all Handlebars templates from the embedded resources. First registers each template as a partial,
        /// then compiles each template individually so partials are available during compilation.
        /// </summary>
        public void Reload()
        {
            lock (_reloadLock)
            {
                // register each before compiling individually so each is available as a partial
                ForEachEmbeddedTemplate(resourceName =>
                {
                    foreach (Assembly assembly in Assemblies)
                    {
                        using (TextReader sr = new StreamReader(assembly.GetManifestResourceStream(resourceName)!))
                        {
                            string longName = Path.GetFileNameWithoutExtension(resourceName);
                            string shortName = longName.Substring(longName.LastIndexOf(".") + 1);
                            string templateText = sr.ReadToEnd();
                            HandlebarsDotNet.Handlebars.RegisterTemplate(longName, templateText);
                            HandlebarsDotNet.Handlebars.RegisterTemplate(shortName, templateText);
                        }
                    }
                });

                ForEachEmbeddedTemplate(resourceName =>
                {
                    foreach (Assembly assembly in Assemblies)
                    {
                        using (TextReader sr = new StreamReader(assembly.GetManifestResourceStream(resourceName)!))
                        {
                            string longName = Path.GetFileNameWithoutExtension(resourceName);
                            string shortName = longName.Substring(longName.LastIndexOf(".") + 1);
                            string templateText = sr.ReadToEnd();

                            HandlebarsTemplate<object, object> compiled = HandlebarsDotNet.Handlebars.Compile(templateText);

                            Templates.TryAdd(longName, compiled);
                            Templates.TryAdd(shortName, compiled);
                        }
                    }
                });

                _loaded = true;
            }
        }

        private void ForEachEmbeddedTemplate(Action<string> action)
        {
            foreach (Assembly assembly in Assemblies)
            {
                string[] resourceNames = assembly.GetManifestResourceNames();
                foreach (string resourceName in resourceNames)
                {
                    if (resourceName.EndsWith(".hbs", StringComparison.InvariantCultureIgnoreCase))
                    {
                        action(resourceName);
                    }
                }
            }
            
        }

    }
}
