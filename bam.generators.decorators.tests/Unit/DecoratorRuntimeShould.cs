using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Generators.Decorators.Tests.Fixtures.Decorators;
using Bam.Logging;
using Bam.Test;
using NSubstitute;
using System.Reflection;

namespace Bam.Generators.Decorators.Tests.Unit
{
    /// <summary>
    /// Pins the split made for bam.generators#14: the runtime the generated decorators link against lives in
    /// its own assembly with no Bam framework dependency, reports through <see cref="IDecoratorLogger"/>, and
    /// the Bam side adapts <see cref="ILogger"/> to it.
    /// </summary>
    [UnitTestMenu("Decorator runtime Should", Selector = "drts")]
    public class DecoratorRuntimeShould : UnitTestMenuContainer
    {
        public DecoratorRuntimeShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void LiveInItsOwnAssemblyWithoutBamDependencies()
        {
            When.A<Assembly>("is the runtime assembly", typeof(Decorator<>).Assembly, runtime =>
            {
                string[] referenced = runtime.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty).ToArray();
                string[] offending = referenced
                    .Where(name => name.StartsWith("bam.", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
                        || name.StartsWith("YamlDotNet", StringComparison.Ordinal)
                        || name.StartsWith("Handlebars", StringComparison.Ordinal))
                    .ToArray();
                return new AssemblyOutcome(
                    runtime.GetName().Name ?? string.Empty,
                    offending,
                    !ReferenceEquals(runtime, typeof(ServiceRegistryExtensions).Assembly),
                    ReferenceEquals(runtime, typeof(IDecoratorLogger).Assembly) && ReferenceEquals(runtime, typeof(DecoratorInvocationContext<EchoService>).Assembly));
            })
            .TheTest
            .ShouldPass<AssemblyOutcome>((because, outcome) =>
            {
                because.ItsTrue("it is bam.generators.decorators.runtime", outcome.Name == "bam.generators.decorators.runtime", outcome.Name);
                because.ItsTrue("it references no Bam framework assembly", outcome.Offending.Length == 0, string.Join(", ", outcome.Offending));
                because.ItsTrue("the registry extensions live elsewhere", outcome.SeparateFromRegistryExtensions);
                because.ItsTrue("the logger abstraction and contexts live with it", outcome.HoldsLoggerAndContext);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FallBackToTheDefaultLoggerWhenGivenNone()
        {
            When.A<EchoService>("is decorated without a logger", new EchoService(), service =>
            {
                IDecoratorLogger previous = DecoratorLogger.Default;
                RecordingLogger recording = new RecordingLogger();
                try
                {
                    DecoratorLogger.Default = recording;
                    Decorator<EchoService> decorator = new Decorator<EchoService>(service);
                    Action<DecoratorInvocationContext<EchoService>> badHandler = context => throw new InvalidOperationException("bad handler");
                    decorator.Subscribe(DecoratorPhase.Start, nameof(IEchoService.Message), badHandler);
                    DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>(nameof(IEchoService.Message), "hello");
                    return new FallbackOutcome(result.Success, result.Value, recording.Messages.ToArray(), recording.Exceptions.ToArray());
                }
                finally
                {
                    DecoratorLogger.Default = previous;
                }
            })
            .TheTest
            .ShouldPass<FallbackOutcome>((because, outcome) =>
            {
                because.ItsTrue("the call still succeeded", outcome.Success && outcome.Value == "hello");
                because.ItsTrue("the failure went to DecoratorLogger.Default", outcome.Messages.Length == 1, $"messages: {outcome.Messages.Length}");
                because.ItsTrue("the message names the phase and method", outcome.Messages.Length == 1 && outcome.Messages[0].Contains("START handler for method Message"), outcome.Messages.FirstOrDefault() ?? "(none)");
                because.ItsTrue("the handler's exception came with it", outcome.Exceptions.Length == 1 && outcome.Exceptions[0] is InvalidOperationException);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReportToTheDefaultInstalledAfterConstruction()
        {
            When.A<EchoService>("is decorated before the host installs its default logger", new EchoService(), service =>
            {
                IDecoratorLogger previous = DecoratorLogger.Default;
                RecordingLogger installedLater = new RecordingLogger();
                try
                {
                    DecoratorLogger.Default = null!;
                    Decorator<EchoService> decorator = new Decorator<EchoService>(service);
                    bool traceAtConstruction = DecoratorLogger.Default is TraceDecoratorLogger;
                    DecoratorLogger.Default = installedLater;
                    Action<DecoratorInvocationContext<EchoService>> badHandler = context => throw new InvalidOperationException("bad handler");
                    decorator.Subscribe(DecoratorPhase.Start, nameof(IEchoService.Message), badHandler);
                    DecoratorInvocationResult<EchoService, string> result = decorator.Invoke<string>(nameof(IEchoService.Message), "hello");
                    return new LateDefaultOutcome(traceAtConstruction, result.Success, result.Value, installedLater.Messages.ToArray());
                }
                finally
                {
                    DecoratorLogger.Default = previous;
                }
            })
            .TheTest
            .ShouldPass<LateDefaultOutcome>((because, outcome) =>
            {
                because.ItsTrue("the Trace logger was the default when the decorator was built", outcome.TraceAtConstruction);
                because.ItsTrue("the call still went ahead", outcome.Success && outcome.Value == "hello");
                because.ItsTrue("the failure reached the default installed after construction", outcome.Messages.Length == 1 && outcome.Messages[0].Contains("bad handler"), string.Join(" | ", outcome.Messages));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RestoreTheTraceLoggerWhenTheDefaultIsCleared()
        {
            When.A<IDecoratorLogger>("is the current default", DecoratorLogger.Default, previous =>
            {
                try
                {
                    DecoratorLogger.Default = new RecordingLogger();
                    bool replaced = DecoratorLogger.Default is RecordingLogger;
                    DecoratorLogger.Default = null!;
                    return new DefaultOutcome(replaced, DecoratorLogger.Default is TraceDecoratorLogger);
                }
                finally
                {
                    DecoratorLogger.Default = previous;
                }
            })
            .TheTest
            .ShouldPass<DefaultOutcome>((because, outcome) =>
            {
                because.ItsTrue("a logger can be installed", outcome.Replaced);
                because.ItsTrue("clearing it restores the Trace logger", outcome.RestoredTrace);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void AdaptTheBamLoggerWithoutFormattingTheMessage()
        {
            When.A<ILogger>("is a Bam logger", Substitute.For<ILogger>(), bamLogger =>
            {
                InvalidOperationException exception = new InvalidOperationException("boom");
                string message = "Decorator<Echo> handler failed: value was {\"json\": 1}";
                BamDecoratorLogger adapter = new BamDecoratorLogger(bamLogger);
                adapter.Error(message, exception);
                adapter.Error(message, null);

                IDecoratorLogger? wrapped = bamLogger.ToDecoratorLogger();
                IDecoratorLogger? none = ((ILogger?)null).ToDecoratorLogger();
                return new AdapterOutcome(
                    bamLogger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Error)),
                    bamLogger.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(ILogger.Error)
                        && call.GetArguments().Length == 3
                        && (string)call.GetArguments()[0]! == "{0}"
                        && ReferenceEquals(call.GetArguments()[1], exception)
                        && call.GetArguments()[2] is object[] args && args.Length == 1 && (string)args[0] == message),
                    bamLogger.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(ILogger.Error)
                        && call.GetArguments().Length == 2
                        && (string)call.GetArguments()[0]! == "{0}"
                        && call.GetArguments()[1] is object[] plain && plain.Length == 1 && (string)plain[0] == message),
                    wrapped is BamDecoratorLogger viaExtension && ReferenceEquals(viaExtension.Logger, bamLogger),
                    none == null);
            })
            .TheTest
            .ShouldPass<AdapterOutcome>((because, outcome) =>
            {
                because.ItsTrue("both failures reached the Bam logger", outcome.ErrorCalls == 2, $"calls: {outcome.ErrorCalls}");
                because.ItsTrue("the exception form passes the message as a format argument, braces intact", outcome.WithException);
                because.ItsTrue("the plain form does the same", outcome.WithoutException);
                because.ItsTrue("ToDecoratorLogger wraps the logger it is given", outcome.Wraps);
                because.ItsTrue("ToDecoratorLogger leaves null as null", outcome.NullStaysNull);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void InstallTheBamDefaultOnlyOverTheTraceLogger()
        {
            When.A<IDecoratorLogger>("is the current default", DecoratorLogger.Default, previous =>
            {
                try
                {
                    DecoratorLogger.Default = null!;
                    BamDecoratorLogger.InstallAsDefault();
                    bool installedOverTrace = DecoratorLogger.Default is BamDecoratorLogger;

                    RecordingLogger custom = new RecordingLogger();
                    DecoratorLogger.Default = custom;
                    BamDecoratorLogger.InstallAsDefault();
                    bool leftCustomAlone = ReferenceEquals(DecoratorLogger.Default, custom);

                    BamDecoratorLogger following = new BamDecoratorLogger();
                    return new InstallOutcome(installedOverTrace, leftCustomAlone, ReferenceEquals(following.Logger, Log.Default));
                }
                finally
                {
                    DecoratorLogger.Default = previous;
                }
            })
            .TheTest
            .ShouldPass<InstallOutcome>((because, outcome) =>
            {
                because.ItsTrue("the Trace logger is replaced", outcome.InstalledOverTrace);
                because.ItsTrue("a logger the host installed is kept", outcome.LeftCustomAlone);
                because.ItsTrue("the parameterless adapter follows Log.Default", outcome.FollowsLogDefault);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReportRegistryDecoratedFailuresToTheRegistryLogger()
        {
            ILogger bamLogger = Substitute.For<ILogger>();

            After.Setup(reg =>
            {
                reg.For<ILogger>().Use(bamLogger);
                reg.For<IDecoratorTypeResolver>().Use(DecoratorTypeResolverShould.NewResolver());
                reg.For<IEchoService>().Use<EchoService>();
                reg.For<ServiceRegistry>().Use(reg);
            })
            .When<ServiceRegistry>("decorates a service and a handler throws", registry =>
            {
                DecoratorRegistration<IEchoService, EchoService> registration = registry.Decorate<IEchoService, EchoService>();
                Action<DecoratorInvocationContext<EchoService>> badHandler = context => throw new InvalidOperationException("bad handler");
                registration.Subscribe(DecoratorPhase.Start, nameof(IEchoService.Message), badHandler);
                IEchoService echo = registry.Get<IEchoService>();
                string result = echo.Message("hello");
                return new RegistryOutcome(
                    result,
                    echo is EchoServiceDecorator,
                    ReferenceEquals(registration.Logger, bamLogger),
                    bamLogger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Error)));
            })
            .TheTest
            .ShouldPass<RegistryOutcome>((because, outcome) =>
            {
                because.ItsTrue("the call went through", outcome.Result == "hello");
                because.ItsTrue("the generated decorator was found by its new constructor shape", outcome.IsGenerated);
                because.ItsTrue("the registration keeps the registry's ILogger", outcome.KeepsRegistryLogger);
                because.ItsTrue("the failure reached the registry's ILogger through the adapter", outcome.ErrorsLogged == 1, $"errors: {outcome.ErrorsLogged}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed class RecordingLogger : IDecoratorLogger
        {
            public List<string> Messages { get; } = new List<string>();

            public List<Exception?> Exceptions { get; } = new List<Exception?>();

            public void Error(string message, Exception? exception)
            {
                Messages.Add(message);
                Exceptions.Add(exception);
            }
        }

        private sealed record AssemblyOutcome(string Name, string[] Offending, bool SeparateFromRegistryExtensions, bool HoldsLoggerAndContext);

        private sealed record LateDefaultOutcome(bool TraceAtConstruction, bool Success, string? Value, string[] Messages);

        private sealed record FallbackOutcome(bool Success, string? Value, string[] Messages, Exception?[] Exceptions);

        private sealed record DefaultOutcome(bool Replaced, bool RestoredTrace);

        private sealed record AdapterOutcome(int ErrorCalls, bool WithException, bool WithoutException, bool Wraps, bool NullStaysNull);

        private sealed record InstallOutcome(bool InstalledOverTrace, bool LeftCustomAlone, bool FollowsLogDefault);

        private sealed record RegistryOutcome(string Result, bool IsGenerated, bool KeepsRegistryLogger, int ErrorsLogged);
    }
}
