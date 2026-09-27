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

        private sealed record ClashOutcome(int CountProperty, int CountMethod, string DescribedByCounter, string DescribedByDescribed);

        private sealed record ResolutionOutcome(Type? Resolved, Type? ResolvedAgain, string Greeting);
    }
}
