using Bam.Console;
using Bam.DependencyInjection;
using Bam.Generators.Client.Tests.Fixtures;
using Bam.Test;

namespace Bam.Generators.Client.Tests.Unit
{
    [UnitTestMenu("BamServiceClientModel should", Selector = "bscms")]
    public class BamServiceClientModelShould : UnitTestMenuContainer
    {
        public BamServiceClientModelShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [UnitTest]
        public void BuildForAValidWebServiceInSubclassMode()
        {
            BamServiceClientModel? model = null;
            When.A<object>("building a subclass model for a valid [WebService]",
                () => new object(),
                (o) =>
                {
                    model = new BamServiceClientModel(typeof(EchoService), GenerationMode.Subclass);
                    return o;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("the model is built", () => model != null);
                because.ItsTrue("it reflects the three remotable methods", () => model!.Methods.Count == 3);
                because.ItsTrue("the client type name is EchoServiceClient", () => model!.ClientTypeName == "EchoServiceClient");
                because.ItsTrue("the base type is fully qualified", () => model!.BaseTypeName == "global::Bam.Generators.Client.Tests.Fixtures.EchoService");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RejectATypeWithoutWebServiceAttribute()
        {
            bool threw = false;
            When.A<object>("building a model for a non-[WebService] type",
                () => new object(),
                (o) =>
                {
                    try { _ = new BamServiceClientModel(typeof(NotAWebService), GenerationMode.Subclass); }
                    catch (ServiceClientGenerationException) { threw = true; }
                    return o;
                })
            .TheTest
            .ShouldPass(because => because.ItsTrue("it throws ServiceClientGenerationException", () => threw))
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RejectNonVirtualMethodsInSubclassMode()
        {
            bool threw = false;
            When.A<object>("building a subclass model for a [WebService] with a non-virtual method",
                () => new object(),
                (o) =>
                {
                    try { _ = new BamServiceClientModel(typeof(SealedEchoService), GenerationMode.Subclass); }
                    catch (ServiceClientGenerationException) { threw = true; }
                    return o;
                })
            .TheTest
            .ShouldPass(because => because.ItsTrue("it throws ServiceClientGenerationException", () => threw))
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void AllowNonVirtualMethodsInInterfaceMode()
        {
            BamServiceClientModel? model = null;
            When.A<object>("building an interface-mode model for a [WebService] with a non-virtual method",
                () => new object(),
                (o) =>
                {
                    model = new BamServiceClientModel(typeof(SealedEchoService), GenerationMode.Interface);
                    return o;
                })
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("the model is built without throwing", () => model != null);
                because.ItsTrue("the mode is Interface", () => model!.Mode == GenerationMode.Interface);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
