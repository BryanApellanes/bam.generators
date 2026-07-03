using Bam.DependencyInjection;
using Bam.Generators.Client.Tests.Fixtures;
using Bam.Test;
using NSubstitute;

namespace Bam.Generators.Client.Tests.Unit
{
    [UnitTestMenu("HandlebarsServiceClientCodeWriter should", Selector = "hsccws")]
    public class HandlebarsServiceClientCodeWriterShould : UnitTestMenuContainer
    {
        public HandlebarsServiceClientCodeWriterShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        private static HandlebarsServiceClientCodeWriter RealWriter()
        {
            // Directory source points at an existing (template-less) dir so only the embedded templates apply.
            return new HandlebarsServiceClientCodeWriter(
                new HandlebarsDirectory(Path.GetTempPath()),
                new HandlebarsEmbeddedResources(typeof(BamServiceClientModel).Assembly));
        }

        [UnitTest]
        public void RenderASubclassClientFromEmbeddedTemplates()
        {
            string source = string.Empty;
            When.A<HandlebarsServiceClientCodeWriter>("renders a subclass client",
                RealWriter,
                (writer) =>
                {
                    source = writer.GetSource(new BamServiceClientModel(typeof(EchoService), GenerationMode.Subclass));
                    return writer;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("declares the client class", () => source.Contains("class EchoServiceClient"));
                because.ItsTrue("subclasses the service type", () => source.Contains(": global::Bam.Generators.Client.Tests.Fixtures.EchoService"));
                because.ItsTrue("emits the private remote-invoke helper", () => source.Contains("InvokeRemoteAsync"));
                because.ItsTrue("selects the transport via the protocol field", () => source.Contains("CreateRequestBuilder(_protocol)"));
                because.ItsTrue("overrides the base method", () => source.Contains("override"));
                because.ItsTrue("emits a synchronous wrapper", () => source.Contains("GetAwaiter().GetResult()"));
                because.ItsTrue("qualifies a generic async return type", () => source.Contains("Task<global::System.Int32>"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RenderAnInterfaceClientFromEmbeddedTemplates()
        {
            string source = string.Empty;
            When.A<HandlebarsServiceClientCodeWriter>("renders an interface client",
                RealWriter,
                (writer) =>
                {
                    source = writer.GetSource(new BamServiceClientModel(typeof(EchoService), GenerationMode.Interface, typeof(IEchoService)));
                    return writer;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("declares the client class", () => source.Contains("class EchoServiceClient"));
                because.ItsTrue("implements the interface", () => source.Contains(": global::Bam.Generators.Client.Tests.Fixtures.IEchoService"));
                because.ItsTrue("does not mark interface members as override", () => !source.Contains("override"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReloadBothTemplateSourcesOnLoad()
        {
            IHandlebarsDirectory mockDirectory = Substitute.For<IHandlebarsDirectory>();
            IHandlebarsEmbeddedResources mockEmbedded = Substitute.For<IHandlebarsEmbeddedResources>();

            When.A<HandlebarsServiceClientCodeWriter>("calls Load",
                new HandlebarsServiceClientCodeWriter(mockDirectory, mockEmbedded),
                (writer) =>
                {
                    writer.Load();
                    return writer;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("reloads the directory source", () => { mockDirectory.Received().Reload(); return true; });
                because.ItsTrue("reloads the embedded source", () => { mockEmbedded.Received().Reload(); return true; });
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
