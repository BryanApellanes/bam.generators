using Bam.DependencyInjection;
using Bam.Test;
using Bam.Generators.Ux.Models;

namespace Bam.Generators.Ux.Tests.Unit
{
    [UnitTestMenu("UxSiteModel should", Selector = "usmut")]
    public class UxSiteModelShould : UnitTestMenuContainer
    {
        public UxSiteModelShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void ComputeFullAddressWithAllParts()
        {
            When.A<UxSiteModel>("computes full address",
                new UxSiteModel
                {
                    Address = "123 Main St",
                    City = "Tyler",
                    State = "TX",
                    Zip = "75701"
                },
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("full address contains street", model.FullAddress.Contains("123 Main St"));
                because.ItsTrue("full address contains city", model.FullAddress.Contains("Tyler"));
                because.ItsTrue("full address contains state", model.FullAddress.Contains("TX"));
                because.ItsTrue("full address contains zip", model.FullAddress.Contains("75701"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReturnAddressOnlyWhenCityIsMissing()
        {
            When.A<UxSiteModel>("returns address when city is empty",
                new UxSiteModel { Address = "123 Main St" },
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("full address equals address", model.FullAddress == "123 Main St");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ComputeNavigationItemsFromPages()
        {
            When.A<UxSiteModel>("computes navigation from pages",
                new UxSiteModel
                {
                    Pages = new List<UxPageModel>
                    {
                        new() { Title = "Home", Slug = "index", InNavigation = true, NavigationOrder = 0 },
                        new() { Title = "Services", Slug = "services", InNavigation = true, NavigationOrder = 1 },
                        new() { Title = "Hidden", Slug = "hidden", InNavigation = false, NavigationOrder = 2 },
                        new() { Title = "Contact", Slug = "contact", InNavigation = true, NavigationOrder = 3 },
                    }
                },
                (model) => model.NavigationItems.ToList())
            .TheTest
            .ShouldPass(because =>
            {
                List<UxNavigationModel> nav = (List<UxNavigationModel>)because.Result;
                because.ItsTrue("has 3 navigation items (hidden excluded)", nav.Count == 3);
                because.ItsTrue("first item is Home", nav[0].Label == "Home");
                because.ItsTrue("home href is /", nav[0].Href == "/");
                because.ItsTrue("services href is /services", nav[1].Href == "/services");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReportHasSocialLinksCorrectly()
        {
            When.A<bool[]>("reports social links presence correctly",
                () =>
                {
                    var withLinks = new UxSiteModel { FacebookUrl = "https://facebook.com/test" };
                    var withoutLinks = new UxSiteModel();
                    return new[] { withLinks.HasSocialLinks, withoutLinks.HasSocialLinks };
                },
                (results) => results)
            .TheTest
            .ShouldPass(because =>
            {
                bool[] results = (bool[])because.Result;
                because.ItsTrue("model with facebook url has social links", results[0]);
                because.ItsTrue("model without urls has no social links", !results[1]);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void InitializeWithDefaults()
        {
            When.A<UxSiteModel>("initializes with safe defaults",
                new UxSiteModel(),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxSiteModel model = (UxSiteModel)because.Result;
                because.ItsTrue("pages is not null", model.Pages != null);
                because.ItsTrue("theme is not null", model.Theme != null);
                because.ItsTrue("industry properties is not null", model.IndustryProperties != null);
                because.ItsTrue("enabled integrations is not null", model.EnabledIntegrations != null);
                because.ItsTrue("company name is empty string", model.CompanyName == string.Empty);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
