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
            HandlebarsCSharpDaoCodeWriter codeWriter = new HandlebarsCSharpDaoCodeWriter(mockHandlebarsDirectory, mockHandlebarsEmbeddedResources);

            codeWriter.Load();

            mockHandlebarsDirectory.Received().Reload();
            mockHandlebarsEmbeddedResources.Received().Reload();
        }
    }
}
