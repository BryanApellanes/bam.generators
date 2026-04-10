namespace Bam.Generators.Ux.Models
{
    public class UxNavigationModel
    {
        public string Label { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int Order { get; set; }
    }
}
