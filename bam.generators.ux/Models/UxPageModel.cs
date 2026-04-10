namespace Bam.Generators.Ux.Models
{
    public class UxPageModel
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public bool InNavigation { get; set; } = true;
        public int NavigationOrder { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
    }
}
