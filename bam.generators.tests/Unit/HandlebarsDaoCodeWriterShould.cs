using Bam.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using Bam.Shell;
using Bam.Console;
using Bam.Net.CommandLine;
using Bam.Services;
using Bam.Net.CoreServices;

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
