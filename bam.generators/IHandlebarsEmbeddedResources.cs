using HandlebarsDotNet;
using System.Reflection;

namespace Bam.Generators
{
    public interface IHandlebarsEmbeddedResources
    {
        Assembly Assembly { get; set; }
        bool IsLoaded { get; }
        Dictionary<string, HandlebarsTemplate<object, object>> Templates { get; set; }

        void Reload();
        string Render(string templateName, object data);
    }
}