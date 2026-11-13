# bam.generators.decorators

Decorators for services in a `ServiceRegistry`: wrap a registered service, then run handlers at the start, end and failure of any of its methods.

The work is split across two projects. [`bam.generators.decorators.runtime`](../bam.generators.decorators.runtime/README.md) (`netstandard2.0` and `net10.0`, no Bam dependency) holds what a generated decorator references: `Decorator<I, T>`, the invocation contexts and results, the handler stores and `IDecoratorLogger`. This project (`net10.0`) holds the `ServiceRegistry` extensions, the registrations, the resolver that finds or compiles a decorator, the generator (`DecoratorGenerator`) that emits a strongly-typed decorator plus typed subscription extension methods, and the adapter from the Bam `ILogger` to the runtime's logger. A .NET Framework application references the runtime package alone and compiles generated decorators against it.

## Usage

```csharp
using Bam.Generators.Decorators;

registry.For<IEchoService>().Use<EchoService>();

// By name, no generated code needed.
registry.OnMethodStart<IEchoService, EchoService>(nameof(IEchoService.Message), ctx =>
{
    // ctx.Args, ctx.Decorated, ctx.Method, ctx.InterfaceMethod
});

// Registry-wide: fires for every decorated service with a method of that name.
// "*" matches every method. Can be subscribed before the services are registered.
registry.OnMethodStart(nameof(IEchoService.Message), ctx => { });

// Typed per-method, from generated code.
registry.OnMessageStart(ctx => { });

IEchoService echo = registry.Get<IEchoService>(); // the decorator
```

The same three phases exist everywhere: `Start`, `End` and `Error`.

A registry-wide handler decorates nothing by itself. It only fires for services that were decorated, either explicitly with `registry.Decorate<I, T>()` or by subscribing a typed handler.

## What decorating does to a registration

Decorating keeps the service's lifetime. `Decorate` puts a `DecoratorRegistration<I, T>` between the registry and the registration it replaces, and that resolves the previous registration each time the service is resolved. A transient service is still constructed per resolve, each instance wrapped in its own decorator. A single instance gets the same decorator every time.

Nothing is constructed when a service is decorated or a handler is subscribed; the service is first resolved when you resolve it. The one exception: a service that was registered again after being decorated is resolved once when you next decorate it, to find out that it was replaced.

Handlers live in three places, and run in this order:

| Subscribed through | Runs for |
|---|---|
| `registry.OnMethodStart(name, ...)` | every decorated service in the registry, including one the registry resolves through another registry's decorator |
| `registry.OnMethodStart<I, T>(...)`, `registry.OnMessageStart(...)`, `registry.Decorate<I, T>().Subscribe(...)` | every instance of that service the registry resolves |
| `echo.OnMessageStart(...)`, `decorator.Subscribe(...)` | that instance only |

A decorator that a second registry resolves through the first serves both registries: it runs both registries' registry-wide handlers, then both registrations' handlers, in the order the registries attached, then its own. Handlers subscribed in the second registry govern calls made by the first registry's consumers too, since the decorator is the same object.

Decorating is serialized per registry. Two threads decorating at once, the same service or different ones, end up with one registration per service and one registry-wide store; a handler subscribed from either thread runs.

A registry's handlers and its record of what it decorated belong to the registry itself, not to its entries. `Include`, `CombineWith` and `CopyFrom` copy services and nothing else: a registry-wide guard subscribed before an `Include` is still there afterward and governs what this registry decorates from then on. An included service that the other registry had decorated arrives as that registry's decorator; it comes under this registry's handlers once this registry decorates it too (or subscribes a typed handler to it). Until then this registry's guard doesn't reach it, and for a transient service it never reaches the other registry's own consumers. A single-instance service is one object, so once this registry decorates and resolves it the decorator serves both registries, as described above, and the other registry's consumers come under this registry's guard too.

Two more things to know. An `Include` that brings a registration for a service this registry had already decorated is a re-registration in bulk: the decorator is replaced like any re-registration, nothing tells you, and `DecoratedTypes` keeps listing the service until it is decorated again (bam.base#9 tracks raising something when a registration is replaced). And a copy of a registry (`Clone`, or a `BamContext` configured from a copy) starts with no handlers and nothing recorded, so decorate and subscribe on the registry you'll resolve from.

Two things to know about ordering:

- Register the service before you decorate it or subscribe a typed handler to it. Only the registry-wide form can come first.
- Registering the service again after decorating it replaces the decorator, and nothing tells you. Decorate it again, or subscribe another typed handler, and the handlers subscribed before are applied to the new registration. Decorating it as a different implementation type while the first decoration is still in place throws.

If the service is registered as something other than a `T`, decorating succeeds and resolving throws a `DecoratorException`. Checking sooner would mean constructing the service.

`ServiceRegistry` reads a registration once per resolve (bam.base#8), so a decorated transient service is constructed and wrapped once per resolve. Decorating adds nothing to that.

## What a handler's return value does

A handler given as an `Action` only observes. A handler given as a `Func` overrides the outcome when it returns a non-null value. Mind how C# binds a lambda: an expression-bodied lambda whose expression has a value, `ctx => seen.Add(ctx.MethodName)` say, is a `Func` returning that value, and on a `void` method any non-null value short-circuits the call. To observe, write a block, `ctx => { seen.Add(ctx.MethodName); }`, which binds to the `Action` overload.

| Phase | A returned value |
|---|---|
| `Start` | short-circuits the call; the decorated method never runs |
| `End` | replaces the value handed to the caller |
| `Error` | is handed to the caller as a fallback; the exception is suppressed |

Setting `ctx.Result` does the same thing and also works for `null`, which is how a handler suppresses a failure in a `void` method. When several handlers override, the last one wins. A value of the wrong type for the method is logged and ignored.

`ctx.Args` is a copy. A handler can read the arguments but changing them doesn't change what the decorated method receives.

## Stopping a call

A handler that throws never breaks the decorated call. The exception is logged and the call goes ahead. That means a guard written as a handler that throws fails open.

To stop a call, reject it:

```csharp
registry.OnMethodStart<IAccountService, AccountService>(nameof(IAccountService.Close), ctx =>
{
    if (!IsAllowed(ctx.Args))
    {
        ctx.Reject(new UnauthorizedAccessException("not allowed"));
    }
});
```

`ctx.Reject(exception)` throws that exception to the caller. `ctx.Reject("reason")` and throwing a `DecoratorRejectionException` from the handler both throw a `DecoratorRejectionException`. At start the decorated method never runs, at end its result is discarded, on error the rejection replaces the failure. No further handlers run on that call, and no error handler on that call can suppress the rejection. (A service that made the rejected call from inside its own decorated method sees an ordinary exception; an error handler on *that* service can still replace what its caller sees. The guarded call didn't run either way.)

Handlers run synchronously, so a rejection has to be made before the handler returns. An `async` handler would return at its first `await` and the call would go ahead, so subscribing one throws `ArgumentException`; every target of a combined delegate is checked. A handler that isn't marked `async` but hands back a `Task`, `ValueTask` or `ValueTask<T>` (`ctx => writer.WriteAsync(...)`, say) is caught on the call instead: the task is not a result, the call is rejected with a `DecoratorException`, and it's logged, so the author finds out on the first call rather than in a log. At start the method never runs; at end its result is discarded; on error the method's own exception is the `InnerException`. Anything with a public `GetAwaiter()` of its own (`ConfigureAwait`, `Task.Yield`, a custom awaitable; not one supplied by an extension method), any type reflection can't answer for (more than one parameterless `GetAwaiter`, generic ones included, or one whose signature fails to load), and a task put on `ctx.Result` by hand, are treated the same way. A typed handler whose declared return type is awaitable (`Subscribe<Task<int>>`) is refused when subscribed, and a method whose result type is itself awaitable (`Task<Task<int>>`) gets no typed `Func` hook. Do the asynchronous work somewhere else and reject from a synchronous handler. A synchronous wrapper around an async delegate passes the subscription check, and whatever it awaits happens after the call went ahead.

A guard only covers what's intercepted. Properties, indexers, events and methods with `ref`/`out`/`in` parameters are forwarded without handlers (see the table below), and a subscription to a name no method has never fires. Check the table before guarding a service by `*`. The runtime doesn't log rejections; a guard that needs an audit trail writes its own.

Two things to keep in mind when a decorated service makes decisions other code relies on. An error handler that returns a value turns a failure into a success, so don't subscribe one to a service that denies by throwing. And a registry-wide handler that returns a value changes the result of every matching method whose return type fits, so subscribe those by method name and keep `*` for handlers that only observe.

## Logging handler failures

A handler or event subscriber that throws, or a handler that supplies a value of the wrong type, is reported through the runtime's `IDecoratorLogger` and the call goes ahead. A decorator constructed by hand takes one in its constructor; without one it uses whatever `DecoratorLogger.Default` is at the time of the failure, so a decorator built before the host installs its default still reports to it. Decorating through a registry passes the registry's `ILogger` (or the one given to `Decorate`) wrapped in `BamDecoratorLogger`, and the first use of `ServiceRegistryExtensions` installs a `BamDecoratorLogger` following `Log.Default` as `DecoratorLogger.Default`, so a Bam host sees the same log lines it did when the runtime depended on `bam.base`. A host that builds decorators by hand and never touches a registry calls `BamDecoratorLogger.InstallAsDefault()` at startup, or wraps a logger with `ILogger.ToDecoratorLogger()`.

## Generating decorators

```csharp
new DecoratorGenerator()
    .AddServiceType<IEchoService, EchoService>()
    .WriteSource("./Generated_Decorators");
```

This writes `EchoServiceDecorator.cs`, holding `EchoServiceDecorator` and `EchoServiceDecoratorExtensions`, in the namespace `{implementation namespace}.Decorators`. The decorator's constructor takes the instance and an optional `IDecoratorLogger`. The decorator class references the runtime package alone. The extensions class holds two kinds of overload for each hook: one on the service instance (`echo.OnMessageStart(...)`), which also references only the runtime, and one on a `ServiceRegistry` (`registry.OnMessageStart(...)`), plus the `DecorateEchoService` method, which reference `bam.base`. `GetSource<I, T>()` renders the same source without writing it.

`WithoutRegistryExtensions()` leaves the `ServiceRegistry` overloads and the `Decorate` method out, so the whole file compiles against the runtime package alone:

```csharp
new DecoratorGenerator()
    .WithoutRegistryExtensions()
    .AddServiceType<IEchoService, EchoService>()
    .WriteSource("./Generated_Decorators");
```

That is the output for a host on another container, or on .NET Framework; see [Using another container](#using-another-container). The option changes nothing else about the file, and the default output is unchanged.

### Decorating an interface alone

A service whose implementation generated code cannot name (an internal class behind a public interface, or a factory registration whose type is chosen at resolve time) is decorated from the interface alone:

```csharp
new DecoratorGenerator()
    .AddInterface<IEchoService>()          // or AddServiceType(typeof(IEchoService), null)
    .WriteSource("./Generated_Decorators");
```

This writes `IEchoServiceDecorator.cs`: `IEchoServiceDecorator : Decorator<IEchoService, IEchoService>, IEchoService`, with a constructor taking an `IEchoService`, so it wraps any instance of the interface. Handlers see `ctx.Method` on the instance's runtime type, mapped through its interface map. In a registry, `registry.Decorate<IEchoService>()` (the same as `Decorate<IEchoService, IEchoService>()`) uses it; `registry.OnMethodStart<IEchoService, IEchoService>(...)` subscribes by name, and the generated `registry.OnMessageStart(...)` hooks work as before. Without a registry, `DecoratorFactory.Create<IEchoService>(instance)` and `DecoratorFactory.Handlers<IEchoService>()` do the same.

One thing differs from an implementation-typed decorator, and it matters for a handler that guards a call. `Decorate<I, T>()` refuses a registration that resolves anything but a `T`, so `ctx.Method` is always the method of a `T`. Here the decorator wraps whatever the registration resolves. When that is a wrapper around the service, or a different implementation, `ctx.Method` is the wrapper's method and the attributes of the real service's methods aren't on it. A guard such as `if (ctx.Method?.GetCustomAttribute<GuardedAttribute>() != null) ctx.Reject(...)` then lets the call through without an error. In this mode, key a guard on the method name or on the interface method's attributes, `ctx.InterfaceMethod?.GetCustomAttribute<GuardedAttribute>()`, never on the implementation's attributes or declaring type. `ctx.InterfaceMethod` is the interface member the call came in through whatever the registration resolves.

The decorator is named after the interface, `I` prefix and all, so it never shares a name with the decorator of an implementation (`EchoServiceDecorator`). Generating both for one interface is allowed. The pair's decorator lives in the implementation's namespace and the interface-only one in the interface's, so wherever both namespaces are in scope the generated `service.OnMessageStart(ctx => ...)` hooks overload each other and an untyped lambda is ambiguous: give the lambda parameter an explicit type, or generate only the one you use. Unwrapping differs too: `(IEchoService)decorator` is a plain reference conversion for a `Decorator<IEchoService, IEchoService>` and returns the decorator, so reach the wrapped instance through `decorator.Instance`. `WriteSource` validates every queued service and checks the output file names before writing any file. Files are named after the decorator type alone, so it refuses two queued services whose decorators would land in the same file, whatever their namespaces (`Billing.IClient` and `Shipping.IClient` both give `IClientDecorator.cs`), since the second would overwrite the first. Write those to separate directories.

The template is embedded (`Templates/Decorator.hbs`). When generating with `DecoratorGenerator`, a `Decorator.hbs` in `./Templates` overrides the embedded one. The template references no partials, since Handlebars partials are registered process-wide.

`registry.Decorate<I, T>()` uses a generated decorator when one is loaded. Otherwise it generates and compiles one on the spot, which costs a compile on first use and needs the service's assembly on disk. Register your own `IDecoratorTypeResolver` in the registry to change that.

The `./Templates` override applies to generation only, not to `Decorate<I, T>()`. What `Decorate` compiles is loaded into the process, so the default resolver renders from the embedded template and nothing else. It reads no template directory and creates none. A resolver you construct with your own `DecoratorGenerator` compiles whatever that generator renders, so give it one that reads templates from disk only when that directory is as trusted as the application's binaries.

## Using another container

A host that does not use `ServiceRegistry` generates with `WithoutRegistryExtensions()`, references the runtime package alone, and creates decorators through `DecoratorFactory` from its own container's decoration hook (SimpleInjector's `ExpressionBuilt` event, `Decorate` in Microsoft.Extensions.DependencyInjection with Scrutor, or a factory lambda). `Create<I, T>` returns the decorator as `I`, the type the hook hands back; the generated `On…` hooks are extension methods on `I`, so they apply to it as returned:

```csharp
using Bam.Generators.Decorators;

// Once, for every decorator the factory creates for the pair.
DecoratorFactory.Handlers<IEchoService, EchoService>()
    .Add(DecoratorPhase.Start, nameof(IEchoService.Message), ctx => { /* ... */ });

// From the container's decoration hook.
IEchoService Decorate(EchoService inner) => DecoratorFactory.Create<IEchoService, EchoService>(inner);

// Or name the generated type, when it is known at compile time.
EchoServiceDecorator decorator = DecoratorFactory.Create<IEchoService, EchoService, EchoServiceDecorator>(inner);
decorator.OnMessageEnd(ctx => { /* this instance only */ });

// When a host is torn down (one process, many hosts: integration tests), empty the pair's store.
// The store is one per pair per process: two live hosts decorating the same pair share it, and a Reset
// by one empties it for both.
DecoratorFactory.Reset<IEchoService, EchoService>();
```

`Create<I, T>` finds the decorator generated for the pair among the loaded assemblies (`GeneratedDecorators.Find`) and remembers it; it compiles nothing, so a pair with no generated decorator loaded throws `DecoratorException`. Handlers added through `DecoratorFactory.Handlers<I, T>()` run before the ones subscribed on the decorator itself, the same order a registry's handlers take. Compiling a decorator on first use stays a `ServiceRegistry` feature.

## What gets intercepted

| Member | Generated code |
|---|---|
| Methods returning a value, `void`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` | intercepted; end and error handlers run after the task completes |
| Generic methods | intercepted, implemented explicitly |
| Methods with `ref`/`out`/`in` or `ref struct` parameters | forwarded, no handlers |
| Properties, indexers, events | forwarded, no handlers |

Members that would clash are implemented explicitly, so they're reachable through the interface only. That covers a name the decorator base already uses (a method named `Invoke`, for example), a signature two inherited interfaces both declare, and a property and a method that share a name.

Each intercepted method gets a static field holding its `MethodInfo`, looked up once by exact parameter types. Handlers are told the overload that ran, as `ctx.Method`, mapped to the method on the decorated instance's class so its attributes are there to read. For a decorator generated from an interface alone that class is whatever instance was wrapped, which may not be the service you have in mind; see [Decorating an interface alone](#decorating-an-interface-alone). A method the runtime cannot map (a generic interface on an array) is reported as the interface's method. `ctx.InterfaceMethod` is the interface method the call came in through, for every decorator with a service interface; it is `ctx.Method` itself on a decorator typed on the implementation alone.

Generated source compiles without warnings under `#nullable enable`. Nullable annotations are read from what the compiler wrote on the interface and carried over wherever they appear, including `T?` on generic methods, inside generic arguments and arrays (`Task<T?>`, `List<T?>`, `T?[]`), and on members of a closed generic interface. Nullability attributes such as `[MaybeNullWhen]` are not carried. Names that are C# keywords are escaped.

Generation fails with a `DecoratorGenerationException` for an interface that isn't public, an open generic, an init-only property, a ref-returning member, a pointer-typed member (including function pointers and pointers inside arrays or generic arguments; generated code isn't `unsafe`), a static abstract member, or a member whose name isn't a valid C# identifier.

Overloads share their hooks, since handlers are selected by method name. Two generated decorators whose services share a method name both declare `registry.OnMessageStart(...)`; if both namespaces are imported, give the lambda parameter an explicit type to pick one.

## Running tests

```bash
dotnet run --project bam.generators.decorators.tests/bam.generators.decorators.tests.csproj -- --ut
```

The runtime's `netstandard2.0` target is proven by `bam.generators.decorators.runtime.compat472`, a .NET Framework 4.7.2 class library that references the runtime project alone and compiles `Fixtures/Generated/NoRegistry/EchoServiceDecorator.cs`, real generator output with `WithoutRegistryExtensions()`, at `LangVersion` 9 (the floor generated output needs). It is built on its own, not from `bamtk.sln`:

```bash
dotnet build bam.generators.decorators.runtime.compat472/bam.generators.decorators.runtime.compat472.csproj
```

`Fixtures/Generated` in the test project holds real generator output, compiled into the tests; `Fixtures/Generated/NoRegistry` holds the same service generated without registry extensions, embedded but not compiled there (it declares the same types), and compiled by `compat472`. Tests compare both to what the generator produces today, so regenerate those files when a template changes:

```bash
bam generate Decorator --config /full/path/to/config.yaml
```

The CLI config gains a `RegistryExtensions: false` switch with bamtk#86; until it lands, regenerate the `NoRegistry` fixture from the generator itself:

```csharp
new DecoratorGenerator().WithoutRegistryExtensions()
    .AddServiceType<IEchoService, EchoService>()
    .WriteSource("bam.generators.decorators.tests/Fixtures/Generated/NoRegistry");
```

The fixtures and the types they decorate are in the same assembly. If a change stops the checked-in fixtures from compiling, the assembly can't be built to regenerate them from. Fix the fixtures by hand far enough to compile, then regenerate over them.
