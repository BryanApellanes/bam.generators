using Bam.Generators.Ux.Models;
using Bam.Logging;

namespace Bam.Generators.Ux
{
    public class HandlebarsUxSiteWriter : Loggable, IUxSiteWriter
    {
        private readonly ITemplateRenderer _renderer;

        public HandlebarsUxSiteWriter(ITemplateRenderer renderer)
        {
            _renderer = renderer;
        }

        public async Task WriteSiteAsync(UxSiteModel model, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            string css = await RenderStylesheetAsync(model.Theme);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "styles.css"), css);

            foreach (UxPageModel page in model.Pages)
            {
                string html = await RenderPageAsync(page, model);
                string fileName = page.Slug == "index" ? "index.html" : $"{page.Slug}.html";
                await File.WriteAllTextAsync(Path.Combine(outputDirectory, fileName), html);
            }

            if (model.AuthEnabled)
            {
                string loginHtml = RenderTemplate("Shared/LoginPage", new { Site = model });
                await File.WriteAllTextAsync(Path.Combine(outputDirectory, "login.html"), loginHtml);
            }
        }

        public Task<string> RenderPageAsync(UxPageModel page, UxSiteModel site)
        {
            string content = RenderTemplate($"Industries/{IndustryFolderName(site.Industry)}/{page.TemplateName}", new
            {
                Site = site,
                Page = page
            });

            string layout = RenderTemplate("Shared/Layout", new
            {
                Site = site,
                PageTitle = page.Title,
                Content = content,
                Navigation = site.NavigationItems.Select(n => new UxNavigationModel
                {
                    Label = n.Label,
                    Href = n.Href,
                    IsActive = n.Href == (page.Slug == "index" ? "/" : $"/{page.Slug}"),
                    Order = n.Order
                })
            });

            return Task.FromResult(layout);
        }

        public Task<string> RenderStylesheetAsync(UxThemeModel theme)
        {
            string css = RenderTemplate("Shared/Styles", new { Theme = theme });
            return Task.FromResult(css);
        }

        private string RenderTemplate(string templateName, object model)
        {
            // Embedded resources use dot-separated names:
            //   "Shared/Layout" → "bam.generators.ux.Templates.Shared.Layout"
            string resourceName = "bam.generators.ux.Templates." + templateName.Replace("/", ".");
            return _renderer.Render(resourceName, model);
        }

        private static string IndustryFolderName(string industry) => industry.ToLowerInvariant() switch
        {
            "healthcare" => "Healthcare",
            "retail" => "Retail",
            "construction" => "Construction",
            "legal" => "Legal",
            "energy" => "Energy",
            "manufacturing" => "Manufacturing",
            _ => industry
        };
    }
}
