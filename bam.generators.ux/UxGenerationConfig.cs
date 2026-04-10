using System.Text.Json;

namespace Bam.Generators.Ux
{
    public class UxGenerationConfig
    {
        public string TemplatePath { get; set; } = ".";
        public string OutputDirectory { get; set; } = "./output";
        public string SiteModelPath { get; set; } = "./site.json";

        public static UxGenerationConfig ReadFile(string path = "./ux-gen.json")
        {
            if (!File.Exists(path))
            {
                return new UxGenerationConfig();
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UxGenerationConfig>(json) ?? new UxGenerationConfig();
        }

        public void WriteFile(string path = "./ux-gen.json")
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }
}
