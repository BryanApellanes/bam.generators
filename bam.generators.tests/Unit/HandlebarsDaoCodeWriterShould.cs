using Bam.DependencyInjection;
using Bam.Test;
using NSubstitute;
using Bam.Services;

namespace Bam.Generators.Tests.Unit
{
    [UnitTestMenu("HandlebarsDaoCodeWriter should")]
    public class HandlebarsDaoCodeWriterShould : UnitTestMenuContainer
    {
        public HandlebarsDaoCodeWriterShould(ServiceRegistry dependencyProvider) : base(dependencyProvider)
        {
        }

        [UnitTest]
        public void CallHandlebarsDirectoryReload()
        {
            IHandlebarsDirectory mockHandlebarsDirectory = Substitute.For<IHandlebarsDirectory>();
            IHandlebarsEmbeddedResources mockHandlebarsEmbeddedResources = Substitute.For<IHandlebarsEmbeddedResources>();

            When.A<HandlebarsCSharpDaoCodeWriter>("calls Load",
                new HandlebarsCSharpDaoCodeWriter(mockHandlebarsDirectory, mockHandlebarsEmbeddedResources),
                (codeWriter) =>
                {
                    codeWriter.Load();
                    return codeWriter;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("Reload was called on HandlebarsDirectory", () => mockHandlebarsDirectory.Received().Reload());
                because.ItsTrue("Reload was called on HandlebarsEmbeddedResources", () => mockHandlebarsEmbeddedResources.Received().Reload());
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
