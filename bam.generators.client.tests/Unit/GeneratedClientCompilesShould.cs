using Bam;
using Bam.DependencyInjection;
using Bam.Generators.Client.Tests.Fixtures;
using Bam.Test;

namespace Bam.Generators.Client.Tests.Unit
{
    /// <summary>
    /// Round-trip guardrail: the source emitted by the generator must actually COMPILE against the
    /// real protocol assemblies. Substring assertions elsewhere prove the shape; this proves the
    /// namespaces, method signatures, and fluent chain the generated code depends on are real.
    /// </summary>
    [UnitTestMenu("Generated client should", Selector = "gccs")]
    public class GeneratedClientCompilesShould : UnitTestMenuContainer
    {
        public GeneratedClientCompilesShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        // The assemblies the generated client references. Passed to RoslynCompiler so their metadata
        // is available; the emitted code is fully qualified with global:: so no usings are needed.
        private static readonly Type[] ReferenceTypes = new[]
        {
            typeof(EchoService),                                 // base type + IEchoService (this test assembly)
            typeof(global::Bam.Protocol.Client.BamClient),       // bam.protocol.client
            typeof(global::Bam.Protocol.MethodInvocationRequest),// bam.protocol
            typeof(global::Newtonsoft.Json.JsonConvert),         // Newtonsoft.Json
        };

        private static void CompileGenerated(GenerationMode mode, Type? iface, out bool compiled, out string diagnostics)
        {
            string source = new BamServiceClientGenerator()
                .AddServiceType(typeof(EchoService), mode, iface)
                .GetSource(typeof(EchoService), mode);

            try
            {
                byte[] assembly = new RoslynCompiler().Compile("GeneratedClient.Test", source, ReferenceTypes);
                compiled = assembly != null && assembly.Length > 0;
                diagnostics = compiled ? string.Empty : "compiler returned no bytes";
            }
            catch (Exception ex)
            {
                compiled = false;
                diagnostics = ex.Message;
            }

            if (!compiled)
            {
                global::System.Console.WriteLine("--- generated source that failed to compile ---");
                global::System.Console.WriteLine(source);
                global::System.Console.WriteLine("--- diagnostics ---");
                global::System.Console.WriteLine(diagnostics);
            }
        }

        [UnitTest]
        public void CompileTheSubclassClient()
        {
            bool compiled = false;
            string diagnostics = string.Empty;
            When.A<object>("compiling the generated subclass client",
                () => new object(),
                (o) =>
                {
                    CompileGenerated(GenerationMode.Subclass, null, out compiled, out diagnostics);
                    return o;
                })
            .TheTest
            .ShouldPass(because => because.ItsTrue("the generated subclass source compiles", () => compiled))
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CompileTheInterfaceClient()
        {
            bool compiled = false;
            string diagnostics = string.Empty;
            When.A<object>("compiling the generated interface client",
                () => new object(),
                (o) =>
                {
                    CompileGenerated(GenerationMode.Interface, typeof(IEchoService), out compiled, out diagnostics);
                    return o;
                })
            .TheTest
            .ShouldPass(because => because.ItsTrue("the generated interface source compiles", () => compiled))
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
