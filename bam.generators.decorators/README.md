# bam.generators.decorators

Decorators for services in a `ServiceRegistry`: wrap a registered service, then run handlers at the start, end and failure of any of its methods.

The project has two halves. The runtime (`Decorator<I, T>` and the extension methods) does the wrapping and runs the handlers. The generator (`DecoratorGenerator`) emits a strongly-typed decorator for a service interface, plus typed extension methods for subscribing to it.

## Usage

```csharp
using Bam.Generators.Decorators;

registry.For<IEchoService>().Use<EchoService>();

// By name, no generated code needed.
registry.OnMethodStart<IEchoService, EchoService>(nameof(IEchoService.Message), ctx =>
{
    // ctx.Args, ctx.Decorated, ctx.Method
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

A registry's handlers and its record of what it decorated belong to the registry itself, not to its entries. `Include`, `CombineWith` and `CopyFrom` copy services and nothing else: a registry-wide guard subscribed before an `Include` is still there afterward and governs what this registry decorates from then on. An included service that the other registry had decorated arrives as that registry's decorator; it comes under this registry's handlers once this registry decorates it too (or subscribes a typed handler to it). Until then this registry's guard doesn't reach it, and for a transient service it never reaches the other registry's own consumers. A single-instance service is one object, so once this registry decorates it the decorator serves both registries, as described above, and the other registry's consumers come under this registry's guard too.

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

Handlers run synchronously, so a rejection has to be made before the handler returns. An `async` handler would return at its first `await` and the call would go ahead, so subscribing one throws `ArgumentException`; every target of a combined delegate is checked. A handler that isn't marked `async` but hands back a `Task`, `ValueTask` or `ValueTask<T>` (`ctx => writer.WriteAsync(...)`, say) is caught on the call instead: the task is not a result, the call is rejected with a `DecoratorException`, and it's logged, so the author finds out on the first call rather than in a log. At start the method never runs; at end its result is discarded; on error the method's own exception is the `InnerException`. Anything else `await` would accept (`ConfigureAwait`, `Task.Yield`, a custom awaitable), and a task put on `ctx.Result` by hand, are treated the same way. A typed handler whose declared return type is awaitable (`Subscribe<Task<int>>`) is refused when subscribed, and a method whose result type is itself awaitable (`Task<Task<int>>`) gets no typed `Func` hook. Do the asynchronous work somewhere else and reject from a synchronous handler. A synchronous wrapper around an async delegate passes the subscription check, and whatever it awaits happens after the call went ahead.

A guard only covers what's intercepted. Properties, indexers, events and methods with `ref`/`out`/`in` parameters are forwarded without handlers (see the table below), and a subscription to a name no method has never fires. Check the table before guarding a service by `*`. The runtime doesn't log rejections; a guard that needs an audit trail writes its own.

Two things to keep in mind when a decorated service makes decisions other code relies on. An error handler that returns a value turns a failure into a success, so don't subscribe one to a service that denies by throwing. And a registry-wide handler that returns a value changes the result of every matching method whose return type fits, so subscribe those by method name and keep `*` for handlers that only observe.

## Generating decorators

```csharp
new DecoratorGenerator()
    .AddServiceType<IEchoService, EchoService>()
    .WriteSource("./Generated_Decorators");
```

This writes `EchoServiceDecorator.cs`, holding `EchoServiceDecorator` and `EchoServiceDecoratorExtensions`, in the namespace `{implementation namespace}.Decorators`. `GetSource<I, T>()` renders the same source without writing it.

The template is embedded (`Templates/Decorator.hbs`). When generating with `DecoratorGenerator`, a `Decorator.hbs` in `./Templates` overrides the embedded one. The template references no partials, since Handlebars partials are registered process-wide.

`registry.Decorate<I, T>()` uses a generated decorator when one is loaded. Otherwise it generates and compiles one on the spot, which costs a compile on first use and needs the service's assembly on disk. Register your own `IDecoratorTypeResolver` in the registry to change that.

The `./Templates` override applies to generation only, not to `Decorate<I, T>()`. What `Decorate` compiles is loaded into the process, so the default resolver renders from the embedded template and nothing else. It reads no template directory and creates none. A resolver you construct with your own `DecoratorGenerator` compiles whatever that generator renders, so give it one that reads templates from disk only when that directory is as trusted as the application's binaries.

## What gets intercepted

| Member | Generated code |
|---|---|
| Methods returning a value, `void`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` | intercepted; end and error handlers run after the task completes |
| Generic methods | intercepted, implemented explicitly |
| Methods with `ref`/`out`/`in` or `ref struct` parameters | forwarded, no handlers |
| Properties, indexers, events | forwarded, no handlers |

Members that would clash are implemented explicitly, so they're reachable through the interface only. That covers a name the decorator base already uses (a method named `Invoke`, for example), a signature two inherited interfaces both declare, and a property and a method that share a name.

Each intercepted method gets a static field holding its `MethodInfo`, looked up once by exact parameter types. Handlers are told the overload that ran, as `ctx.Method`, mapped to the method on the decorated class so its attributes are there to read.

Generated source compiles without warnings under `#nullable enable`. Nullable annotations are read from what the compiler wrote on the interface and carried over wherever they appear, including `T?` on generic methods, inside generic arguments and arrays (`Task<T?>`, `List<T?>`, `T?[]`), and on members of a closed generic interface. Nullability attributes such as `[MaybeNullWhen]` are not carried. Names that are C# keywords are escaped.

Generation fails with a `DecoratorGenerationException` for an interface that isn't public, an open generic, an init-only property, a ref-returning member, a pointer-typed member (including function pointers and pointers inside arrays or generic arguments; generated code isn't `unsafe`), a static abstract member, or a member whose name isn't a valid C# identifier.

Overloads share their hooks, since handlers are selected by method name. Two generated decorators whose services share a method name both declare `registry.OnMessageStart(...)`; if both namespaces are imported, give the lambda parameter an explicit type to pick one.

## Running tests

```bash
dotnet run --project bam.generators.decorators.tests/bam.generators.decorators.tests.csproj -- --ut
```

`Fixtures/Generated` in the test project holds real generator output, compiled into the tests. One test compares it to what the generator produces today, so regenerate those files when a template changes:

```bash
bam generate Decorator --config /full/path/to/config.yaml
```

The fixtures and the types they decorate are in the same assembly. If a change stops the checked-in fixtures from compiling, the assembly can't be built to regenerate them from. Fix the fixtures by hand far enough to compile, then regenerate over them.
