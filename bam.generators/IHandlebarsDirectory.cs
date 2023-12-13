using Bam.Net.Logging;

namespace Bam.Generators
{
    public interface IHandlebarsDirectory
    {
        DirectoryInfo Directory { get; set; }
        string FileExtension { get; set; }
        bool IsLoaded { get; }
        ILogger Logger { get; }
        HashSet<DirectoryInfo> PartialsDirectories { get; set; }
        Dictionary<string, Func<object, string>> Templates { get; }

        void AddCompiledTemplateFile(FileInfo file);
        void AddPartial(string templateName, string source, bool reload = false);
        void AddPartialsDirectory(string partialsDirectory);
        void AddTemplate(string templateName, string source, bool reload = false);
        HandlebarsDirectory CombineWith(params HandlebarsDirectory[] dirs);
        bool HasTemplate(string templateName);
        void Load(bool reload);
        void Reload();
        string Render(string templateName, object data);
    }
}