using Bam.Generators.Ux.Models;

namespace Bam.Generators.Ux
{
    public interface IUxSiteWriter
    {
        Task WriteSiteAsync(UxSiteModel model, string outputDirectory);
        Task<string> RenderPageAsync(UxPageModel page, UxSiteModel site);
        Task<string> RenderStylesheetAsync(UxThemeModel theme);
    }
}
