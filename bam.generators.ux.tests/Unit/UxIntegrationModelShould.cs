using Bam.DependencyInjection;
using Bam.Test;
using Bam.Generators.Ux.Models;

namespace Bam.Generators.Ux.Tests.Unit
{
    [UnitTestMenu("UxIntegrationModel should", Selector = "uimut")]
    public class UxIntegrationModelShould : UnitTestMenuContainer
    {
        public UxIntegrationModelShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void InitializeWithEmptyDefaults()
        {
            When.A<UxIntegrationModel>("initializes with safe defaults",
                new UxIntegrationModel(),
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxIntegrationModel model = (UxIntegrationModel)because.Result;
                because.ItsTrue("system id is empty string", model.SystemId == string.Empty);
                because.ItsTrue("system name is empty string", model.SystemName == string.Empty);
                because.ItsTrue("link text is empty string", model.LinkText == string.Empty);
                because.ItsTrue("link url is empty string", model.LinkUrl == string.Empty);
                because.ItsTrue("admin only defaults to false", model.AdminOnly == false);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void StoreIntegrationLinkProperties()
        {
            When.A<UxIntegrationModel>("stores integration link properties",
                new UxIntegrationModel
                {
                    SystemId = "hipaa-compliance-manager",
                    SystemName = "HIPAA Compliance Manager",
                    Industry = "healthcare",
                    LinkText = "Manage HIPAA Compliance",
                    LinkUrl = "https://healthcare.threeheadz.com/hipaa-compliance-manager?client=acme-clinic",
                    AdminOnly = true
                },
                (model) => model)
            .TheTest
            .ShouldPass(because =>
            {
                UxIntegrationModel model = (UxIntegrationModel)because.Result;
                because.ItsTrue("system id stored", model.SystemId == "hipaa-compliance-manager");
                because.ItsTrue("link url contains client param", model.LinkUrl.Contains("client=acme-clinic"));
                because.ItsTrue("admin only is true", model.AdminOnly);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
