using Bam.Generators.Ux.Models;

namespace Bam.Generators.Ux
{
    public class UxSiteGenerator
    {
        private readonly IUxSiteWriter _writer;

        public UxSiteGenerator(IUxSiteWriter writer)
        {
            _writer = writer;
        }

        public Task GenerateAsync(UxSiteModel model, string outputPath)
        {
            return _writer.WriteSiteAsync(model, outputPath);
        }

        public Task<string> RenderPageAsync(UxPageModel page, UxSiteModel site)
        {
            return _writer.RenderPageAsync(page, site);
        }

        public Task<string> RenderStylesheetAsync(UxSiteModel site)
        {
            return _writer.RenderStylesheetAsync(site.Theme);
        }

        public static UxSiteModel CreateDefaultModel(string companyName, string industry)
        {
            string slug = ToKebabCase(companyName);
            UxThemeModel theme = ColorPaletteGenerator.Suggest(companyName, industry);

            var model = new UxSiteModel
            {
                CompanyName = companyName,
                CompanySlug = slug,
                Industry = industry,
                Theme = theme,
                Pages = GetDefaultPages(industry)
            };

            return model;
        }

        private static List<UxPageModel> GetDefaultPages(string industry) => industry.ToLowerInvariant() switch
        {
            "healthcare" => new()
            {
                new() { Title = "Home", Slug = "index", TemplateName = "Index", NavigationOrder = 0 },
                new() { Title = "Services", Slug = "services", TemplateName = "Services", NavigationOrder = 1 },
                new() { Title = "Compliance", Slug = "compliance", TemplateName = "Compliance", NavigationOrder = 2 },
                new() { Title = "Patient Resources", Slug = "patient-resources", TemplateName = "PatientResources", NavigationOrder = 3 },
                new() { Title = "Contact", Slug = "contact", TemplateName = "Contact", NavigationOrder = 4, InNavigation = true },
            },
            "retail" => new()
            {
                new() { Title = "Home", Slug = "index", TemplateName = "Index", NavigationOrder = 0 },
                new() { Title = "Services", Slug = "services", TemplateName = "Services", NavigationOrder = 1 },
                new() { Title = "Menu", Slug = "menu", TemplateName = "Menu", NavigationOrder = 2 },
                new() { Title = "Locations", Slug = "locations", TemplateName = "Locations", NavigationOrder = 3 },
                new() { Title = "Contact", Slug = "contact", TemplateName = "Contact", NavigationOrder = 4 },
            },
            "construction" => new()
            {
                new() { Title = "Home", Slug = "index", TemplateName = "Index", NavigationOrder = 0 },
                new() { Title = "Services", Slug = "services", TemplateName = "Services", NavigationOrder = 1 },
                new() { Title = "Projects", Slug = "projects", TemplateName = "Projects", NavigationOrder = 2 },
                new() { Title = "Safety", Slug = "safety", TemplateName = "SafetyCompliance", NavigationOrder = 3 },
                new() { Title = "Contact", Slug = "contact", TemplateName = "Contact", NavigationOrder = 4 },
            },
            "legal" => new()
            {
                new() { Title = "Home", Slug = "index", TemplateName = "Index", NavigationOrder = 0 },
                new() { Title = "Practice Areas", Slug = "practice-areas", TemplateName = "PracticeAreas", NavigationOrder = 1 },
                new() { Title = "Our Team", Slug = "team", TemplateName = "Team", NavigationOrder = 2 },
                new() { Title = "Client Portal", Slug = "client-portal", TemplateName = "ClientPortal", NavigationOrder = 3 },
                new() { Title = "Contact", Slug = "contact", TemplateName = "Contact", NavigationOrder = 4 },
            },
            "energy" => new()
            {
                new() { Title = "Home", Slug = "index", TemplateName = "Index", NavigationOrder = 0 },
                new() { Title = "Services", Slug = "services", TemplateName = "Services", NavigationOrder = 1 },
                new() { Title = "Field Operations", Slug = "field-operations", TemplateName = "FieldOperations", NavigationOrder = 2 },
                new() { Title = "Safety", Slug = "safety", TemplateName = "SafetyCompliance", NavigationOrder = 3 },
                new() { Title = "Contact", Slug = "contact", TemplateName = "Contact", NavigationOrder = 4 },
            },
            "manufacturing" => new()
            {
                new() { Title = "Home", Slug = "index", TemplateName = "Index", NavigationOrder = 0 },
                new() { Title = "Capabilities", Slug = "capabilities", TemplateName = "Capabilities", NavigationOrder = 1 },
                new() { Title = "Quality", Slug = "quality", TemplateName = "QualityCertifications", NavigationOrder = 2 },
                new() { Title = "Facilities", Slug = "facilities", TemplateName = "Facilities", NavigationOrder = 3 },
                new() { Title = "Contact", Slug = "contact", TemplateName = "Contact", NavigationOrder = 4 },
            },
            _ => new()
            {
                new() { Title = "Home", Slug = "index", TemplateName = "Index", NavigationOrder = 0 },
                new() { Title = "Services", Slug = "services", TemplateName = "Services", NavigationOrder = 1 },
                new() { Title = "Contact", Slug = "contact", TemplateName = "Contact", NavigationOrder = 4 },
            }
        };

        private static string ToKebabCase(string value)
        {
            return value.Trim()
                .ToLowerInvariant()
                .Replace("&", "and")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace(" ", "-")
                .Replace("--", "-")
                .TrimEnd('-');
        }
    }
}
