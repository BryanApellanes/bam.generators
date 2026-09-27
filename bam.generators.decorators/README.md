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

Decorating resolves the current registration once and registers the decorator as an instance. A service registered as transient resolves to the same decorated instance from then on.

A registry-wide handler decorates nothing by itself. It only fires for services that were decorated, either explicitly with `registry.Decorate<I, T>()` or by subscribing a typed handler.

## What a handler's return value does

A handler given as an `Action` only observes. A handler given as a `Func` overrides the outcome when it returns a non-null value.

| Phase | A returned value |
|---|---|
| `Start` | short-circuits the call; the decorated method never runs |
| `End` | replaces the value handed to the caller |
| `Error` | is handed to the caller as a fallback; the exception is suppressed |

Setting `ctx.Result` does the same thing and also works for `null`, which is how a handler suppresses a failure in a `void` method. When several handlers override, the last one wins. A value of the wrong type for the method is logged and ignored.

A handler that throws never breaks the decorated call. The exception is logged and the call continues.

## Generating decorators

```csharp
new DecoratorGenerator()
    .AddServiceType<IEchoService, EchoService>()
    .WriteSource("./Generated_Decorators");
```

This writes `EchoServiceDecorator.cs`, holding `EchoServiceDecorator` and `EchoServiceDecoratorExtensions`, in the namespace `{implementation namespace}.Decorators`. `GetSource<I, T>()` renders the same source without writing it.

Templates are embedded (`Templates/*.hbs`). A template of the same name in `./Templates` overrides the embedded one.

`registry.Decorate<I, T>()` uses a generated decorator when one is loaded. Otherwise it generates and compiles one on the spot, which costs a compile on first use and needs the service's assembly on disk. Register your own `IDecoratorTypeResolver` in the registry to change that.

## What gets intercepted

| Member | Generated code |
|---|---|
| Methods returning a value, `void`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` | intercepted; end and error handlers run after the task completes |
| Generic methods | intercepted, implemented explicitly |
| Methods with `ref`/`out`/`in` or `ref struct` parameters | forwarded, no handlers |
| Properties, indexers, events | forwarded, no handlers |

Members that would clash with the decorator base (a method named `Invoke`, for example) are implemented explicitly, so they're reachable through the interface only.

Generation fails with a `DecoratorGenerationException` for an interface that isn't public, an open generic, an init-only property, a ref-returning member, or a static abstract member.

Overloads share their hooks, since handlers are selected by method name. Two generated decorators whose services share a method name both declare `registry.OnMessageStart(...)`; if both namespaces are imported, give the lambda parameter an explicit type to pick one.

## Running tests

```bash
dotnet run --project bam.generators.decorators.tests/bam.generators.decorators.tests.csproj -- --ut
```

`Fixtures/Generated` in the test project holds real generator output, compiled into the tests. One test compares it to what the generator produces today, so regenerate those files when a template changes.
