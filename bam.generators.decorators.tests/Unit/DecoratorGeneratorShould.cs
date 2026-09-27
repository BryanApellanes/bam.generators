using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Test;

namespace Bam.Generators.Decorators.Tests.Unit
{
    [UnitTestMenu("DecoratorGenerator Should", Selector = "dgs")]
    public class DecoratorGeneratorShould : UnitTestMenuContainer
    {
        public DecoratorGeneratorShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        internal static DecoratorGenerator NewGenerator()
        {
            return new DecoratorGenerator(HandlebarsDecoratorCodeWriterShould.RealWriter(), new FsDecoratorTargetResolver());
        }

        [UnitTest]
        public void ReturnSourceForAService()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorGenerator>().Use(NewGenerator());
            })
            .When<DecoratorGenerator>("gets source for a service", generator =>
            {
                return new SourceOutcome(generator.GetSource(typeof(IEchoService), typeof(EchoService)), generator.GetSource<IEchoService, EchoService>());
            })
            .TheTest
            .ShouldPass<SourceOutcome>((because, outcome) =>
            {
                because.ItsTrue("the source declares the decorator", outcome.Source.Contains("class EchoServiceDecorator"));
                because.ItsTrue("the generic overload renders the same source", outcome.Source == outcome.GenericSource);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void WriteOneFilePerQueuedService()
        {
            string outputDirectory = Path.Combine(Path.GetTempPath(), "bgd-" + Guid.NewGuid().ToString("N"));

            After.Setup(reg =>
            {
                reg.For<DecoratorGenerator>().Use(NewGenerator());
            })
            .When<DecoratorGenerator>("writes queued services", generator =>
            {
                IReadOnlyList<string> written = generator
                    .AddServiceType<IEchoService, EchoService>()
                    .AddServiceType(typeof(IKitchenSinkService), typeof(KitchenSinkService))
                    .AddServiceType<IEchoService, EchoService>()
                    .WriteSource(outputDirectory);

                string echoFile = Path.Combine(outputDirectory, "EchoServiceDecorator.cs");
                string sinkFile = Path.Combine(outputDirectory, "KitchenSinkServiceDecorator.cs");
                return new WriteOutcome(
                    string.Join("|", written.Select(path => Path.GetFileName(path))),
                    File.Exists(echoFile) && File.ReadAllText(echoFile).Contains("class EchoServiceDecorator"),
                    File.Exists(sinkFile) && File.ReadAllText(sinkFile).Contains("class KitchenSinkServiceDecorator"),
                    Directory.GetFiles(outputDirectory).Length);
            })
            .TheTest
            .ShouldPass<WriteOutcome>((because, outcome) =>
            {
                because.ItsTrue("the written paths are returned, a repeated service once", outcome.Written == "EchoServiceDecorator.cs|KitchenSinkServiceDecorator.cs", $"written: {outcome.Written}");
                because.ItsTrue("the echo decorator was written", outcome.EchoWritten);
                because.ItsTrue("the kitchen sink decorator was written", outcome.SinkWritten);
                because.ItsTrue("nothing else was written", outcome.FileCount == 2);
            })
            .SoBeHappy(reg =>
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            })
            .UnlessItFailed();
        }

        [UnitTest]
        public void WriteToACallerSuppliedStream()
        {
            After.Setup(reg =>
            {
                reg.For<FsDecoratorTargetResolver>().Use(new FsDecoratorTargetResolver());
            })
            .When<FsDecoratorTargetResolver>("is given a stream resolver", resolver =>
            {
                string? requested = null;
                using MemoryStream supplied = new MemoryStream();
                Stream resolved = resolver.GetTargetDecoratorStream(
                    fileName =>
                    {
                        requested = fileName;
                        return supplied;
                    },
                    "unused",
                    new DecoratorModel(typeof(IEchoService), typeof(EchoService)));
                return new ResolverOutcome(requested, ReferenceEquals(resolved, supplied), Directory.Exists("unused"));
            })
            .TheTest
            .ShouldPass<ResolverOutcome>((because, outcome) =>
            {
                because.ItsTrue("the resolver is asked for the decorator's file name", outcome.RequestedFileName == "EchoServiceDecorator.cs");
                because.ItsTrue("the supplied stream is used", outcome.UsedSuppliedStream);
                because.ItsTrue("no directory is created", !outcome.CreatedDirectory);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RefuseAServiceThatCannotBeDecorated()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorGenerator>().Use(NewGenerator());
            })
            .When<DecoratorGenerator>("is asked for a pair that cannot be decorated", generator =>
            {
                try
                {
                    return new SourceOutcome(generator.GetSource(typeof(IEchoService), typeof(NotAnEchoService)), string.Empty);
                }
                catch (DecoratorGenerationException ex)
                {
                    return new SourceOutcome("threw for " + ex.InterfaceType.Name, string.Empty);
                }
            })
            .TheTest
            .ShouldPass<SourceOutcome>((because, outcome) =>
            {
                because.ItsTrue("it throws DecoratorGenerationException naming the interface", outcome.Source == "threw for IEchoService");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record SourceOutcome(string Source, string GenericSource);

        private sealed record WriteOutcome(string Written, bool EchoWritten, bool SinkWritten, int FileCount);

        private sealed record ResolverOutcome(string? RequestedFileName, bool UsedSuppliedStream, bool CreatedDirectory);
    }
}
