using Bam.DependencyInjection;
using Bam.Generators.Decorators.Tests.Fixtures;
using Bam.Test;
using System.Reflection;
using System.Reflection.Emit;

namespace Bam.Generators.Decorators.Tests.Unit
{
    [UnitTestMenu("DecoratorModel Should", Selector = "dms")]
    public class DecoratorModelShould : UnitTestMenuContainer
    {
        public DecoratorModelShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        private const string FixtureNamespace = "global::Bam.Generators.Decorators.Tests.Fixtures";

        [UnitTest]
        public void NameTheGeneratedTypes()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorModel>().Use(new DecoratorModel(typeof(IEchoService), typeof(EchoService)));
            })
            .When<DecoratorModel>("is built for a service", model =>
            {
                return new NamingOutcome(model.Namespace, model.DecoratorTypeName, model.ExtensionsTypeName, model.DecorateMethodName, model.FileName, model.DecoratorFullName, model.InterfaceName, model.ImplementationName, model.BaseTypeName, model.ContextTypeName);
            })
            .TheTest
            .ShouldPass<NamingOutcome>((because, outcome) =>
            {
                because.ItsTrue("the namespace is the implementation's plus .Decorators", outcome.Namespace == "Bam.Generators.Decorators.Tests.Fixtures.Decorators");
                because.ItsTrue("the decorator is named after the implementation", outcome.DecoratorTypeName == "EchoServiceDecorator");
                because.ItsTrue("the extensions class is named after the decorator", outcome.ExtensionsTypeName == "EchoServiceDecoratorExtensions");
                because.ItsTrue("the decorate method is named after the implementation", outcome.DecorateMethodName == "DecorateEchoService");
                because.ItsTrue("the file is named after the decorator", outcome.FileName == "EchoServiceDecorator.cs");
                because.ItsTrue("the full name joins namespace and type", outcome.DecoratorFullName == "Bam.Generators.Decorators.Tests.Fixtures.Decorators.EchoServiceDecorator");
                because.ItsTrue("the interface is fully qualified", outcome.InterfaceName == FixtureNamespace + ".IEchoService");
                because.ItsTrue("the implementation is fully qualified", outcome.ImplementationName == FixtureNamespace + ".EchoService");
                because.ItsTrue("the base type closes Decorator<I, T>", outcome.BaseTypeName == $"global::Bam.Generators.Decorators.Decorator<{FixtureNamespace}.IEchoService, {FixtureNamespace}.EchoService>");
                because.ItsTrue("the context closes DecoratorInvocationContext<T>", outcome.ContextTypeName == $"global::Bam.Generators.Decorators.DecoratorInvocationContext<{FixtureNamespace}.EchoService>");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReflectEveryMemberIncludingInheritedOnes()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorModel>().Use(new DecoratorModel(typeof(IKitchenSinkService), typeof(KitchenSinkService)));
            })
            .When<DecoratorModel>("is built for an interface that inherits another", model =>
            {
                return new MemberOutcome(
                    string.Join(",", model.Properties.Select(property => property.IsIndexer ? "this" : property.Name)),
                    string.Join(",", model.Events.Select(eventModel => eventModel.Name)),
                    string.Join(",", model.Methods.Select(method => method.Name)),
                    model.Properties.Single(property => property.Name == "Name").DeclaringInterface);
            })
            .TheTest
            .ShouldPass<MemberOutcome>((because, outcome) =>
            {
                because.ItsTrue("properties and the indexer are found, inherited ones last", outcome.Properties == "Count,this,Name", $"properties: {outcome.Properties}");
                because.ItsTrue("events are found", outcome.Events == "Changed");
                because.ItsTrue("methods are found, accessors excluded",
                    outcome.Methods == "Add,Add,Reset,Find,AddAsync,ResetAsync,DescribeAsync,FlushAsync,TryParse,Echo,Fail,FailAsync,Invoke",
                    $"methods: {outcome.Methods}");
                because.ItsTrue("an inherited member is attributed to the interface declaring it", outcome.NameDeclaredOn == typeof(INamedService));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ClassifyMethods()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorModel>().Use(new DecoratorModel(typeof(IKitchenSinkService), typeof(KitchenSinkService)));
            })
            .When<DecoratorModel>("is built for methods of every shape", model =>
            {
                DecoratorMethodModel Method(string name)
                {
                    return model.Methods.First(method => method.Name == name);
                }

                return new ClassificationOutcome(
                    Method("Reset").Kind,
                    Method("Add").Kind,
                    Method("ResetAsync").Kind,
                    Method("AddAsync").Kind,
                    Method("FlushAsync").Kind,
                    Method("DescribeAsync").Kind,
                    Method("AddAsync").ResultType,
                    Method("Find").ResultTypeName,
                    Method("TryParse").IsIntercepted,
                    Method("Add").IsIntercepted,
                    Method("Echo").IsExplicit,
                    Method("Invoke").IsExplicit,
                    Method("Add").IsExplicit,
                    Method("AddAsync").IsAsync,
                    Method("Add").IsAsync);
            })
            .TheTest
            .ShouldPass<ClassificationOutcome>((because, outcome) =>
            {
                because.ItsTrue("void is Void", outcome.Reset == DecoratorMethodKind.Void);
                because.ItsTrue("a value is Value", outcome.Add == DecoratorMethodKind.Value);
                because.ItsTrue("Task is Task", outcome.ResetAsync == DecoratorMethodKind.Task);
                because.ItsTrue("Task<T> is TaskOfResult", outcome.AddAsync == DecoratorMethodKind.TaskOfResult);
                because.ItsTrue("ValueTask is ValueTask", outcome.FlushAsync == DecoratorMethodKind.ValueTask);
                because.ItsTrue("ValueTask<T> is ValueTaskOfResult", outcome.DescribeAsync == DecoratorMethodKind.ValueTaskOfResult);
                because.ItsTrue("the result of Task<int> is int", outcome.AddAsyncResultType == typeof(int));
                because.ItsTrue("nullable annotations are kept", outcome.FindResultTypeName == "global::System.String?");
                because.ItsTrue("a method with an out parameter is forwarded, not intercepted", !outcome.TryParseIntercepted);
                because.ItsTrue("an ordinary method is intercepted", outcome.AddIntercepted);
                because.ItsTrue("a generic method is implemented explicitly", outcome.EchoExplicit);
                because.ItsTrue("a method named like a decorator member is implemented explicitly", outcome.InvokeExplicit);
                because.ItsTrue("an ordinary method is public", !outcome.AddExplicit);
                because.ItsTrue("task-returning methods await", outcome.AddAsyncIsAsync && !outcome.AddIsAsync);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void OfferTypedHooksWhereOverloadsAgree()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorModel>().Use(new DecoratorModel(typeof(IKitchenSinkService), typeof(KitchenSinkService)));
            })
            .When<DecoratorModel>("builds its hooks", model =>
            {
                DecoratorHookModel Hook(string name)
                {
                    return model.Hooks.First(hook => hook.HookName == name);
                }

                return new HookOutcome(
                    model.Hooks.Count,
                    model.Hooks.Count(hook => hook.MethodName == "Add"),
                    Hook("OnAddStart").HasResult,
                    Hook("OnFindEnd").ResultTypeName,
                    Hook("OnAddAsyncError").ResultTypeName,
                    Hook("OnFailStart").ResultTypeName,
                    Hook("OnResetStart").HasResult,
                    Hook("OnEchoStart").HasResult,
                    model.Hooks.Any(hook => hook.MethodName == "TryParse"),
                    Hook("OnFailError").Phase);
            })
            .TheTest
            .ShouldPass<HookOutcome>((because, outcome) =>
            {
                because.ItsTrue("there is one hook per intercepted method name and phase", outcome.HookCount == 33, $"hooks: {outcome.HookCount}");
                because.ItsTrue("overloads share their hooks", outcome.AddHookCount == 3);
                because.ItsTrue("overloads that disagree on a result type get no typed handler", !outcome.AddHasResult);
                because.ItsTrue("an already nullable result is left as is", outcome.FindResultTypeName == "global::System.String?");
                because.ItsTrue("a value-type result becomes nullable", outcome.AddAsyncResultTypeName == "global::System.Int32?");
                because.ItsTrue("a reference-type result becomes nullable", outcome.FailResultTypeName == "global::System.String?");
                because.ItsTrue("a void method gets no typed handler", !outcome.ResetHasResult);
                because.ItsTrue("a generic result gets no typed handler", !outcome.EchoHasResult);
                because.ItsTrue("a forwarded method gets no hooks", !outcome.TryParseHasHooks);
                because.ItsTrue("the phase is named", outcome.FailErrorPhase == "Error");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NameAClosedGenericService()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorModel>().Use(new DecoratorModel(typeof(IBoxService<string>), typeof(BoxService<string>)));
            })
            .When<DecoratorModel>("is built for a closed generic service", model =>
            {
                return new NamingOutcome(model.Namespace, model.DecoratorTypeName, model.ExtensionsTypeName, model.DecorateMethodName, model.FileName, model.DecoratorFullName, model.InterfaceName, model.ImplementationName, model.BaseTypeName, model.ContextTypeName);
            })
            .TheTest
            .ShouldPass<NamingOutcome>((because, outcome) =>
            {
                because.ItsTrue("the type arguments are folded into the name", outcome.DecoratorTypeName == "BoxServiceOfStringDecorator");
                because.ItsTrue("the decorate method follows", outcome.DecorateMethodName == "DecorateBoxServiceOfString");
                because.ItsTrue("the interface keeps its type arguments", outcome.InterfaceName == FixtureNamespace + ".IBoxService<global::System.String>");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RejectPairsThatCannotBeDecorated()
        {
            When.A<DecoratorModelShould>("builds models for unsupported pairs", this, test =>
            {
                return new RejectionOutcome(
                    Rejects(typeof(EchoService), typeof(EchoService)),
                    Rejects(typeof(IEchoService), typeof(NotAnEchoService)),
                    Rejects(typeof(IHiddenService), typeof(HiddenService)),
                    Rejects(typeof(IInitOnlyService), typeof(InitOnlyService)),
                    Rejects(typeof(IBoxService<>), typeof(BoxService<>)),
                    Rejects(typeof(IEchoService), typeof(IEchoService)),
                    Rejects(typeof(IPointerService), typeof(PointerService)));
            })
            .TheTest
            .ShouldPass<RejectionOutcome>((because, outcome) =>
            {
                because.ItsTrue("a class in place of the interface is rejected", outcome.NotAnInterface != null);
                because.ItsTrue("an implementation that does not implement the interface is rejected", outcome.NotImplemented != null);
                because.ItsTrue("non-public types are rejected", outcome.NotPublic != null);
                because.ItsTrue("an init-only property is rejected", outcome.InitOnly != null && outcome.InitOnly.Contains("init-only"));
                because.ItsTrue("an open generic is rejected", outcome.OpenGeneric != null);
                because.ItsTrue("an interface in place of the implementation is rejected", outcome.NotAClass != null);
                because.ItsTrue("a pointer-typed member is rejected up front rather than by the compiler", outcome.Pointer != null && outcome.Pointer.Contains("pointer"), outcome.Pointer);
                because.ItsTrue("the message names the interface", outcome.NotImplemented!.StartsWith("[" + typeof(IEchoService).FullName + "]"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ImplementClashingMembersExplicitly()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorModel>().Use(new DecoratorModel(typeof(IClashService), typeof(ClashService)));
            })
            .When<DecoratorModel>("is built for interfaces whose members clash", model =>
            {
                return new ClashOutcome(
                    model.Properties.Single(property => property.Name == "Count").IsExplicit,
                    model.Methods.Single(method => method.Name == "Count").IsExplicit,
                    string.Join(",", model.Methods.Where(method => method.Name == "Describe").Select(method => method.DeclaringInterface.Name + ":" + method.IsExplicit)),
                    model.Hooks.Count(hook => hook.MethodName == "Describe"));
            })
            .TheTest
            .ShouldPass<ClashOutcome>((because, outcome) =>
            {
                because.ItsTrue("the first member to take a name is public", !outcome.PropertyExplicit);
                because.ItsTrue("a member of another kind with that name is explicit", outcome.MethodExplicit);
                because.ItsTrue("a signature declared twice is public once and explicit once", outcome.Describes == "ICounter:False,IDescribed:True", $"describes: {outcome.Describes}");
                because.ItsTrue("both share one set of hooks", outcome.DescribeHooks == 3);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RejectNamesThatAreNotIdentifiers()
        {
            When.A<DecoratorModelShould>("builds a model for a member no C# source could declare", this, test =>
            {
                // Only IL can carry such a name, so the types are emitted rather than written.
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Hostile.Names"), AssemblyBuilderAccess.Run);
                ModuleBuilder module = assembly.DefineDynamicModule("Hostile.Names");
                string hostileName = "Run\"); global::System.Environment.Exit(1); //";

                TypeBuilder interfaceBuilder = module.DefineType("Hostile.IHostile", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
                interfaceBuilder.DefineMethod(hostileName, MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot, typeof(void), Type.EmptyTypes);
                Type hostileInterface = interfaceBuilder.CreateType();

                TypeBuilder implementationBuilder = module.DefineType("Hostile.Hostile", TypeAttributes.Public | TypeAttributes.Class);
                implementationBuilder.AddInterfaceImplementation(hostileInterface);
                MethodBuilder method = implementationBuilder.DefineMethod(hostileName, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final, typeof(void), Type.EmptyTypes);
                method.GetILGenerator().Emit(OpCodes.Ret);
                Type hostileImplementation = implementationBuilder.CreateType();

                return new RejectionOutcome(Rejects(hostileInterface, hostileImplementation), null, null, null, null, null, null);
            })
            .TheTest
            .ShouldPass<RejectionOutcome>((because, outcome) =>
            {
                because.ItsTrue("the pair is rejected rather than written into source", outcome.NotAnInterface?.Contains("is not a valid C# identifier") == true, outcome.NotAnInterface);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReferenceOneTypePerAssembly()
        {
            After.Setup(reg =>
            {
                reg.For<DecoratorModel>().Use(new DecoratorModel(typeof(IKitchenSinkService), typeof(KitchenSinkService)));
            })
            .When<DecoratorModel>("lists the types generated source depends on", model =>
            {
                Type[] referenced = model.ReferencedTypes;
                return new ReferenceOutcome(
                    referenced.Select(type => type.Assembly).Distinct().Count() == referenced.Length,
                    referenced.Any(type => type.Assembly == typeof(IKitchenSinkService).Assembly),
                    referenced.Any(type => type.Assembly == typeof(Decorator<>).Assembly),
                    referenced.Any(type => type.Assembly == typeof(ServiceRegistry).Assembly),
                    referenced.Any(type => type.IsGenericParameter));
            })
            .TheTest
            .ShouldPass<ReferenceOutcome>((because, outcome) =>
            {
                because.ItsTrue("each assembly appears once", outcome.OnePerAssembly);
                because.ItsTrue("the service's assembly is referenced", outcome.ReferencesService);
                because.ItsTrue("the decorator runtime is referenced", outcome.ReferencesRuntime);
                because.ItsTrue("the framework base is referenced", outcome.ReferencesBase);
                because.ItsTrue("generic parameters are not listed", !outcome.ListsGenericParameters);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static string? Rejects(Type interfaceType, Type implementationType)
        {
            try
            {
                DecoratorModel model = new DecoratorModel(interfaceType, implementationType);
                return null;
            }
            catch (DecoratorGenerationException ex)
            {
                return ex.Message;
            }
        }

        private sealed record NamingOutcome(string Namespace, string DecoratorTypeName, string ExtensionsTypeName, string DecorateMethodName, string FileName, string DecoratorFullName, string InterfaceName, string ImplementationName, string BaseTypeName, string ContextTypeName);

        private sealed record MemberOutcome(string Properties, string Events, string Methods, Type NameDeclaredOn);

        private sealed record ClassificationOutcome(
            DecoratorMethodKind Reset,
            DecoratorMethodKind Add,
            DecoratorMethodKind ResetAsync,
            DecoratorMethodKind AddAsync,
            DecoratorMethodKind FlushAsync,
            DecoratorMethodKind DescribeAsync,
            Type? AddAsyncResultType,
            string? FindResultTypeName,
            bool TryParseIntercepted,
            bool AddIntercepted,
            bool EchoExplicit,
            bool InvokeExplicit,
            bool AddExplicit,
            bool AddAsyncIsAsync,
            bool AddIsAsync);

        private sealed record HookOutcome(
            int HookCount,
            int AddHookCount,
            bool AddHasResult,
            string FindResultTypeName,
            string AddAsyncResultTypeName,
            string FailResultTypeName,
            bool ResetHasResult,
            bool EchoHasResult,
            bool TryParseHasHooks,
            string FailErrorPhase);

        private sealed record RejectionOutcome(string? NotAnInterface, string? NotImplemented, string? NotPublic, string? InitOnly, string? OpenGeneric, string? NotAClass, string? Pointer);

        private sealed record ClashOutcome(bool PropertyExplicit, bool MethodExplicit, string Describes, int DescribeHooks);

        private sealed record ReferenceOutcome(bool OnePerAssembly, bool ReferencesService, bool ReferencesRuntime, bool ReferencesBase, bool ListsGenericParameters);
    }
}
