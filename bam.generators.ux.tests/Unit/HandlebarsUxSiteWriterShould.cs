using Bam.DependencyInjection;
using Bam.Test;
using Bam.Generators.Ux;
using Bam.Generators.Ux.Models;
using NSubstitute;

namespace Bam.Generators.Ux.Tests.Unit
{
    [UnitTestMenu("HandlebarsUxSiteWriter should", Selector = "huswut")]
    public class HandlebarsUxSiteWriterShould : UnitTestMenuContainer
    {
        public HandlebarsUxSiteWriterShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void CallRendererForStylesheet()
        {
            ITemplateRenderer mockRenderer = Substitute.For<ITemplateRenderer>();
            mockRenderer.Render(Arg.Any<string>(), Arg.Any<object>()).Returns("body { color: red; }");

            When.A<HandlebarsUxSiteWriter>("renders stylesheet via template renderer",
                new HandlebarsUxSiteWriter(mockRenderer),
                (writer) =>
                {
                    var theme = new UxThemeModel { PrimaryColor = "#FF0000" };
                    string css = writer.RenderStylesheetAsync(theme).GetAwaiter().GetResult();
                    return css;
                })
            .TheTest
            .ShouldPass(because =>
            {
                string css = (string)because.Result;
                because.ItsTrue("renderer was called", css == "body { color: red; }");
                mockRenderer.Received().Render(Arg.Is<string>(s => s.Contains("Styles")), Arg.Any<object>());
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CallRendererForPage()
        {
            ITemplateRenderer mockRenderer = Substitute.For<ITemplateRenderer>();
            mockRenderer.Render(Arg.Any<string>(), Arg.Any<object>()).Returns("<h1>Test</h1>");

            When.A<HandlebarsUxSiteWriter>("renders page via template renderer",
                new HandlebarsUxSiteWriter(mockRenderer),
                (writer) =>
                {
                    var site = new UxSiteModel
                    {
                        CompanyName = "Test Co",
                        Industry = "healthcare",
                        Pages = new List<UxPageModel>
                        {
                            new() { Title = "Home", Slug = "index", TemplateName = "Index", InNavigation = true }
                        }
                    };
                    var page = site.Pages[0];
                    string html = writer.RenderPageAsync(page, site).GetAwaiter().GetResult();
                    return html;
                })
            .TheTest
            .ShouldPass(because =>
            {
                string html = (string)because.Result;
                because.ItsTrue("returned rendered content", !string.IsNullOrEmpty(html));
                because.ItsTrue("renderer was called for industry template",
                    () => mockRenderer.Received().Render(
                        Arg.Is<string>(s => s.Contains("Healthcare")),
                        Arg.Any<object>()));
                because.ItsTrue("renderer was called for layout",
                    () => mockRenderer.Received().Render(
                        Arg.Is<string>(s => s.Contains("Layout")),
                        Arg.Any<object>()));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void WriteSiteCreatesOutputDirectory()
        {
            ITemplateRenderer mockRenderer = Substitute.For<ITemplateRenderer>();
            mockRenderer.Render(Arg.Any<string>(), Arg.Any<object>()).Returns("<html></html>");

            string tempDir = Path.Combine(Path.GetTempPath(), $"bam-ux-test-{Guid.NewGuid():N}");

            When.A<HandlebarsUxSiteWriter>("creates output directory when writing site",
                new HandlebarsUxSiteWriter(mockRenderer),
                (writer) =>
                {
                    var model = UxSiteGenerator.CreateDefaultModel("Test", "healthcare");
                    writer.WriteSiteAsync(model, tempDir).GetAwaiter().GetResult();
                    return tempDir;
                })
            .TheTest
            .ShouldPass(because =>
            {
                string dir = (string)because.Result;
                because.ItsTrue("output directory was created", Directory.Exists(dir));
                because.ItsTrue("styles.css was written", File.Exists(Path.Combine(dir, "styles.css")));
                because.ItsTrue("index.html was written", File.Exists(Path.Combine(dir, "index.html")));

                // Cleanup
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void WriteSiteCreatesLoginPageWhenAuthEnabled()
        {
            ITemplateRenderer mockRenderer = Substitute.For<ITemplateRenderer>();
            mockRenderer.Render(Arg.Any<string>(), Arg.Any<object>()).Returns("<html></html>");

            string tempDir = Path.Combine(Path.GetTempPath(), $"bam-ux-test-{Guid.NewGuid():N}");

            When.A<HandlebarsUxSiteWriter>("creates login page when auth enabled",
                new HandlebarsUxSiteWriter(mockRenderer),
                (writer) =>
                {
                    var model = UxSiteGenerator.CreateDefaultModel("Test", "healthcare");
                    model.AuthEnabled = true;
                    writer.WriteSiteAsync(model, tempDir).GetAwaiter().GetResult();
                    return tempDir;
                })
            .TheTest
            .ShouldPass(because =>
            {
                string dir = (string)because.Result;
                because.ItsTrue("login.html was written", File.Exists(Path.Combine(dir, "login.html")));

                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NotCreateLoginPageWhenAuthDisabled()
        {
            ITemplateRenderer mockRenderer = Substitute.For<ITemplateRenderer>();
            mockRenderer.Render(Arg.Any<string>(), Arg.Any<object>()).Returns("<html></html>");

            string tempDir = Path.Combine(Path.GetTempPath(), $"bam-ux-test-{Guid.NewGuid():N}");

            When.A<HandlebarsUxSiteWriter>("skips login page when auth disabled",
                new HandlebarsUxSiteWriter(mockRenderer),
                (writer) =>
                {
                    var model = UxSiteGenerator.CreateDefaultModel("Test", "healthcare");
                    model.AuthEnabled = false;
                    writer.WriteSiteAsync(model, tempDir).GetAwaiter().GetResult();
                    return tempDir;
                })
            .TheTest
            .ShouldPass(because =>
            {
                string dir = (string)because.Result;
                because.ItsTrue("login.html was not written", !File.Exists(Path.Combine(dir, "login.html")));

                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
