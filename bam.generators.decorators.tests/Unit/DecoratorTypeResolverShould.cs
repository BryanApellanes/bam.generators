using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Logging;
using Bam.Test;
using NSubstitute;

namespace Bam.Generators.Decorators.Tests.Unit
{
    [UnitTestMenu("DecoratorTypeResolver Should", Selector = "dtrs")]
    public class DecoratorTypeResolverShould : UnitTestMenuContainer
    {
        public DecoratorTypeResolverShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        internal static DecoratorTypeResolver NewResolver()
        {
            return new DecoratorTypeResolver(DecoratorGeneratorShould.NewGenerator());
        }

        [UnitTest]
        public void FindADecoratorGeneratedAheadOfTime()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorTypeResolver>().Use(NewResolver());
            })
            .When<DecoratorTypeResolver>("resolves a pair whose decorator is already loaded", resolver =>
            {
                return new ResolutionOutcome(resolver.Resolve(typeof(IEchoService), typeof(EchoService)), null, string.Empty);
            })
            .TheTest
            .ShouldPass<ResolutionOutcome>((because, outcome) =>
            {
                because.ItsTrue("the generated decorator is used, not a fresh compile", outcome.Resolved == typeof(EchoServiceDecorator), $"resolved: {outcome.Resolved?.AssemblyQualifiedName}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CompileADecoratorThatDoesNotExistYet()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorTypeResolver>().Use(NewResolver());
            })
            .When<DecoratorTypeResolver>("resolves a pair with no generated decorator", resolver =>
            {
                Type first = resolver.Resolve(typeof(IGreeterService), typeof(GreeterService));
                Type second = resolver.Resolve(typeof(IGreeterService), typeof(GreeterService));
                IGreeterService greeter = (IGreeterService)Activator.CreateInstance(first, new GreeterService(), Substitute.For<ILogger>())!;
                return new ResolutionOutcome(first, second, greeter.Greet("bam"));
            })
            .TheTest
            .ShouldPass<ResolutionOutcome>((because, outcome) =>
            {
                because.ItsTrue("a decorator type is produced", outcome.Resolved?.FullName == "Bam.Generators.Decorators.Tests.Fixtures.Decorators.GreeterServiceDecorator");
                because.ItsTrue("it extends the decorator base", typeof(Decorator<IGreeterService, GreeterService>).IsAssignableFrom(outcome.Resolved));
                because.ItsTrue("it implements the service interface and forwards to the service", outcome.Greeting == "Hello, bam");
                because.ItsTrue("the pair is compiled once and cached", ReferenceEquals(outcome.Resolved, outcome.ResolvedAgain));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CompileMembersThatClash()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorTypeResolver>().Use(NewResolver());
            })
            .When<DecoratorTypeResolver>("resolves a service whose inherited members clash", resolver =>
            {
                Type decoratorType = resolver.Resolve(typeof(IClashService), typeof(ClashService));
                IClashService clash = (IClashService)Activator.CreateInstance(decoratorType, new ClashService(), Substitute.For<ILogger>())!;
                return new ClashOutcome(((ICounted)clash).Count, ((ICounter)clash).Count(), ((ICounter)clash).Describe(), ((IDescribed)clash).Describe());
            })
            .TheTest
            .ShouldPass<ClashOutcome>((because, outcome) =>
            {
                because.ItsTrue("the property reaches the service's property", outcome.CountProperty == 7);
                because.ItsTrue("the method of the same name reaches the service's method", outcome.CountMethod == 11);
                because.ItsTrue("a signature declared twice reaches the service through either interface", outcome.DescribedByCounter == "clash" && outcome.DescribedByDescribed == "clash");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest(RunSynchronously = true)]
        public void IgnoreTemplatesInTheWorkingDirectory()
        {
            // What the resolver compiles is loaded into the process, so a file in the working directory must
            // never decide what that is. Plant a template where the build-time writer would look for one.
            string workingDirectory = Path.Combine(Path.GetTempPath(), "bam-decorator-cwd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(workingDirectory, "Templates"));
            File.WriteAllText(Path.Combine(workingDirectory, "Templates", "Decorator.hbs"), "// planted template: {{{DecoratorTypeName}}}");
            string original = Environment.CurrentDirectory;

            When.A<DecoratorTypeResolverShould>("resolves through a default resolver with a template planted in the working directory", this, test =>
            {
                Environment.CurrentDirectory = workingDirectory;
                try
                {
                    DecoratorTypeResolver resolver = new DecoratorTypeResolver();
                    Type decoratorType = resolver.Resolve(typeof(IPingService), typeof(PingService));
                    IPingService ping = (IPingService)Activator.CreateInstance(decoratorType, new PingService(), Substitute.For<ILogger>())!;
                    string rendered = resolver.Generator.GetSource(typeof(IPingService), typeof(PingService));
                    string planted = new DecoratorGenerator().GetSource(typeof(IPingService), typeof(PingService));
                    return new WorkingDirectoryOutcome(ping.Ping(), rendered.Contains("planted template"), planted.Contains("planted template"), false);
                }
                finally
                {
                    Environment.CurrentDirectory = original;
                }
            })
            .TheTest
            .ShouldPass<WorkingDirectoryOutcome>((because, outcome) =>
            {
                because.ItsTrue("the decorator was compiled from the embedded template and works", outcome.Result == "pong");
                because.ItsTrue("the resolver's generator does not render the planted template", !outcome.ResolverUsedPlanted);
                because.ItsTrue("the build-time generator still honors the override, so the plant was real", outcome.BuildTimeUsedPlanted);
            })
            .SoBeHappy(reg =>
            {
                Directory.Delete(workingDirectory, true);
            })
            .UnlessItFailed();
        }

        [UnitTest(RunSynchronously = true)]
        public void NotCreateATemplatesDirectory()
        {
            string workingDirectory = Path.Combine(Path.GetTempPath(), "bam-decorator-cwd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);
            string original = Environment.CurrentDirectory;

            When.A<DecoratorTypeResolverShould>("compiles a decorator through a default resolver", this, test =>
            {
                Environment.CurrentDirectory = workingDirectory;
                try
                {
                    Type decoratorType = new DecoratorTypeResolver().Resolve(typeof(IPongService), typeof(PongService));
                    IPongService pong = (IPongService)Activator.CreateInstance(decoratorType, new PongService(), Substitute.For<ILogger>())!;
                    return new WorkingDirectoryOutcome(pong.Pong(), false, false, Directory.EnumerateFileSystemEntries(workingDirectory).Any());
                }
                finally
                {
                    Environment.CurrentDirectory = original;
                }
            })
            .TheTest
            .ShouldPass<WorkingDirectoryOutcome>((because, outcome) =>
            {
                because.ItsTrue("the decorator was compiled and works", outcome.Result == "ping");
                because.ItsTrue("nothing was created in the working directory", !outcome.WorkingDirectoryTouched);
            })
            .SoBeHappy(reg =>
            {
                Directory.Delete(workingDirectory, true);
            })
            .UnlessItFailed();
        }

        [UnitTest]
        public void TryAgainAfterAFailure()
        {
            After.Setup(reg =>
            {
                reg.For<FailsOnceResolver>().Use(new FailsOnceResolver());
            })
            .When<FailsOnceResolver>("fails to compile a pair, then is asked again", resolver =>
            {
                Exception? first = null;
                try
                {
                    resolver.Resolve(typeof(IEchoService), typeof(EchoService));
                }
                catch (Exception ex)
                {
                    first = ex;
                }

                Type second = resolver.Resolve(typeof(IEchoService), typeof(EchoService));
                Type third = resolver.Resolve(typeof(IEchoService), typeof(EchoService));
                return new RetryOutcome(first, second, third, resolver.Compiles);
            })
            .TheTest
            .ShouldPass<RetryOutcome>((because, outcome) =>
            {
                because.ItsTrue("the first attempt fails", outcome.First is DecoratorGenerationException);
                because.ItsTrue("the failure is not remembered", outcome.Second == typeof(EchoServiceDecorator));
                because.ItsTrue("a success is", outcome.Third == typeof(EchoServiceDecorator) && outcome.Compiles == 2);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RefuseAPairThatCannotBeDecorated()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorTypeResolver>().Use(NewResolver());
            })
            .When<DecoratorTypeResolver>("resolves a pair that cannot be decorated", resolver =>
            {
                try
                {
                    return new ResolutionOutcome(resolver.Resolve(typeof(IEchoService), typeof(NotAnEchoService)), null, string.Empty);
                }
                catch (DecoratorGenerationException ex)
                {
                    return new ResolutionOutcome(null, null, "threw for " + ex.InterfaceType.Name);
                }
            })
            .TheTest
            .ShouldPass<ResolutionOutcome>((because, outcome) =>
            {
                because.ItsTrue("it throws DecoratorGenerationException", outcome.Greeting == "threw for IEchoService", outcome.Greeting);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        /// <summary>Finds nothing loaded and fails its first compile, to exercise the retry path.</summary>
        public class FailsOnceResolver : DecoratorTypeResolver
        {
            public int Compiles { get; private set; }

            protected override Type? FindGenerated(Type interfaceType, Type implementationType)
            {
                return null;
            }

            protected override Type Compile(Type interfaceType, Type implementationType)
            {
                Compiles++;
                if (Compiles == 1)
                {
                    throw new DecoratorGenerationException(interfaceType, "first compile fails");
                }

                return typeof(EchoServiceDecorator);
            }
        }

        private sealed record WorkingDirectoryOutcome(string Result, bool ResolverUsedPlanted, bool BuildTimeUsedPlanted, bool WorkingDirectoryTouched);

        private sealed record RetryOutcome(Exception? First, Type Second, Type Third, int Compiles);

        private sealed record ClashOutcome(int CountProperty, int CountMethod, string DescribedByCounter, string DescribedByDescribed);

        private sealed record ResolutionOutcome(Type? Resolved, Type? ResolvedAgain, string Greeting);
    }
}
