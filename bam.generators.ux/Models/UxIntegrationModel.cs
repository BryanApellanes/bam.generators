namespace Bam.Generators.Ux.Models
{
    public class UxIntegrationModel
    {
        public string SystemId { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string LinkText { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public bool AdminOnly { get; set; }
    }
}
