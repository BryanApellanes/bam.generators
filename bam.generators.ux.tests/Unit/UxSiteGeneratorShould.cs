using Bam.DependencyInjection;
using Bam.Test;
using Bam.Generators.Ux;
using Bam.Generators.Ux.Models;

namespace Bam.Generators.Ux.Tests.Unit
{
    [UnitTestMenu("UxSiteGenerator should", Selector = "usgut")]
    public class UxSiteGeneratorShould : UnitTestMenuContainer
    {
        public UxSiteGeneratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void CreateDefaultModelForHealthcare()
        {
            When.A<UxSiteModel>("creates default model for healthcare",
                () => UxSiteGenerator.CreateDefaultModel("Acme Clinic", "healthcare"),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("company name is set", model.CompanyName == "Acme Clinic");
                because.ItsTrue("slug is kebab-cased", model.CompanySlug == "acme-clinic");
                because.ItsTrue("industry is healthcare", model.Industry == "healthcare");
                because.ItsTrue("has pages", model.Pages.Count > 0);
                because.ItsTrue("has home page", model.Pages.Any(p => p.Slug == "index"));
                because.ItsTrue("has services page", model.Pages.Any(p => p.Slug == "services"));
                because.ItsTrue("has compliance page", model.Pages.Any(p => p.Slug == "compliance"));
                because.ItsTrue("theme is populated", !string.IsNullOrEmpty(model.Theme.PrimaryColor));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CreateDefaultModelForRetail()
        {
            When.A<UxSiteModel>("creates default model for retail",
                () => UxSiteGenerator.CreateDefaultModel("Joe's Pizza", "retail"),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("slug handles apostrophe", model.CompanySlug == "joes-pizza");
                because.ItsTrue("has menu page", model.Pages.Any(p => p.Slug == "menu"));
                because.ItsTrue("has locations page", model.Pages.Any(p => p.Slug == "locations"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CreateDefaultModelForConstruction()
        {
            When.A<UxSiteModel>("creates default model for construction",
                () => UxSiteGenerator.CreateDefaultModel("Smith & Sons", "construction"),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("slug handles ampersand", model.CompanySlug == "smith-and-sons");
                because.ItsTrue("has projects page", model.Pages.Any(p => p.Slug == "projects"));
                because.ItsTrue("has safety page", model.Pages.Any(p => p.Slug == "safety"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CreateDefaultModelForLegal()
        {
            When.A<UxSiteModel>("creates default model for legal",
                () => UxSiteGenerator.CreateDefaultModel("Johnson Law Firm", "legal"),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("has practice areas page", model.Pages.Any(p => p.Slug == "practice-areas"));
                because.ItsTrue("has team page", model.Pages.Any(p => p.Slug == "team"));
                because.ItsTrue("has client portal page", model.Pages.Any(p => p.Slug == "client-portal"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CreateDefaultModelForEnergy()
        {
            When.A<UxSiteModel>("creates default model for energy",
                () => UxSiteGenerator.CreateDefaultModel("East Texas Energy", "energy"),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("has field operations page", model.Pages.Any(p => p.Slug == "field-operations"));
                because.ItsTrue("has safety page", model.Pages.Any(p => p.Slug == "safety"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CreateDefaultModelForManufacturing()
        {
            When.A<UxSiteModel>("creates default model for manufacturing",
                () => UxSiteGenerator.CreateDefaultModel("Precision Parts Inc", "manufacturing"),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("has capabilities page", model.Pages.Any(p => p.Slug == "capabilities"));
                because.ItsTrue("has quality page", model.Pages.Any(p => p.Slug == "quality"));
                because.ItsTrue("has facilities page", model.Pages.Any(p => p.Slug == "facilities"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CreateDefaultModelForUnknownIndustryWithBasicPages()
        {
            When.A<UxSiteModel>("creates default model for unknown industry",
                () => UxSiteGenerator.CreateDefaultModel("Test Corp", "unknown"),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("has home page", model.Pages.Any(p => p.Slug == "index"));
                because.ItsTrue("has contact page", model.Pages.Any(p => p.Slug == "contact"));
                because.ItsTrue("has at least 2 pages", model.Pages.Count >= 2);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void SetNavigationOrderOnDefaultPages()
        {
            When.A<UxSiteModel>("sets navigation order on pages",
                () => UxSiteGenerator.CreateDefaultModel("Test", "healthcare"),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                var ordered = model.Pages.OrderBy(p => p.NavigationOrder).ToList();
                because.ItsTrue("first page is home", ordered[0].Slug == "index");
                because.ItsTrue("pages have sequential order",
                    ordered.Select(p => p.NavigationOrder).Distinct().Count() == ordered.Count);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
