namespace Bam.Generators.Ux.Models
{
    public class UxSiteModel
    {
        // ── Company Identity ────────────────────────────────────────────
        public string CompanyName { get; set; } = string.Empty;
        public string CompanySlug { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public string FaviconUrl { get; set; } = string.Empty;

        // ── Contact Info ────────────────────────────────────────────────
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;

        // ── Theme ───────────────────────────────────────────────────────
        public UxThemeModel Theme { get; set; } = new();

        // ── Pages ───────────────────────────────────────────────────────
        public List<UxPageModel> Pages { get; set; } = new();

        // ── Social Links ────────────────────────────────────────────────
        public string? FacebookUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? GoogleBusinessUrl { get; set; }

        // ── Industry-Specific Properties ────────────────────────────────
        public Dictionary<string, string> IndustryProperties { get; set; } = new();

        // ── Integration Links (to threeheadz.com systems) ──────────────
        public List<UxIntegrationModel> EnabledIntegrations { get; set; } = new();

        // ── Auth ────────────────────────────────────────────────────────
        public bool AuthEnabled { get; set; }
        public string? AuthApiUrl { get; set; }

        // ── Computed ────────────────────────────────────────────────────
        public string FullAddress => string.IsNullOrEmpty(City)
            ? Address
            : $"{Address}, {City}, {State} {Zip}".Trim();

        public IEnumerable<UxNavigationModel> NavigationItems =>
            Pages.Where(p => p.InNavigation)
                .OrderBy(p => p.NavigationOrder)
                .Select(p => new UxNavigationModel
                {
                    Label = p.Title,
                    Href = p.Slug == "index" ? "/" : $"/{p.Slug}",
                    Order = p.NavigationOrder
                });

        public bool HasSocialLinks =>
            !string.IsNullOrEmpty(FacebookUrl) ||
            !string.IsNullOrEmpty(LinkedInUrl) ||
            !string.IsNullOrEmpty(TwitterUrl) ||
            !string.IsNullOrEmpty(GoogleBusinessUrl);
    }
}
