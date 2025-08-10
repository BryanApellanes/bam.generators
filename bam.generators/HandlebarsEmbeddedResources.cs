using System.Reflection;
using HandlebarsDotNet;

namespace Bam.Generators
{
    public class HandlebarsEmbeddedResources : IHandlebarsEmbeddedResources
    {
        public HandlebarsEmbeddedResources(params Assembly[] assemblies)
        {
            Assemblies = assemblies;
            Templates = new Dictionary<string, HandlebarsTemplate<object, object>>();
        }

        public HandlebarsEmbeddedResources(IEnumerable<Assembly> assemblies)
        {
            Assemblies = assemblies;
            Templates = new Dictionary<string, HandlebarsTemplate<object, object>>();   
        }
        
        public IEnumerable<Assembly> Assemblies { get; set; }

        public Dictionary<string, HandlebarsTemplate<object, object>> Templates
        {
            get;
            set;
        }

        readonly object _reloadLock = new object();
        bool _loaded = false;
        public bool IsLoaded => _loaded;

        public string Render(string templateName, object data)
        {
            if (!_loaded)
            {
                Reload();
            }
            if (!Templates.ContainsKey(templateName))
            {
                Args.Throw<InvalidOperationException>("Specified template not found: {0}", templateName);
            }
            return Templates[templateName](data);
        }

        public void Reload()
        {
            lock (_reloadLock)
            {
                // register each before compiling individually so each is available as a partial
                ForEachEmbeddedTemplate(resourceName =>
                {
                    foreach (Assembly assembly in Assemblies)
                    {
                        using (TextReader sr = new StreamReader(assembly.GetManifestResourceStream(resourceName)))
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
                        using (TextReader sr = new StreamReader(assembly.GetManifestResourceStream(resourceName)))
                        {
                            string longName = Path.GetFileNameWithoutExtension(resourceName);
                            string shortName = longName.Substring(longName.LastIndexOf(".") + 1);
                            string templateText = sr.ReadToEnd();

                            HandlebarsTemplate<object, object> compiled = HandlebarsDotNet.Handlebars.Compile(templateText);

                            Templates.AddMissing(longName, compiled);
                            Templates.AddMissing(shortName, compiled);
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
