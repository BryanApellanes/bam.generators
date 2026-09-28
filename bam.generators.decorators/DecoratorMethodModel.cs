using System.Reflection;
using System.Text;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Handlebars render model for one interface method. An intercepted method is routed through the decorator
    /// base so its start, end and error handlers run; a method that cannot be intercepted is forwarded straight
    /// to the decorated instance.
    /// </summary>
    public class DecoratorMethodModel : DecoratorMemberModel
    {
        /// <summary>Initializes the model by reflecting over <paramref name="method"/>.</summary>
        /// <param name="declaringInterface">The interface declaring the method.</param>
        /// <param name="method">The reflected method.</param>
        /// <param name="isExplicit">Whether to render an explicit interface implementation.</param>
        public DecoratorMethodModel(Type declaringInterface, MethodInfo method, bool isExplicit)
            : base(declaringInterface, method, isExplicit)
        {
            Method = method;
            Parameters = method.GetParameters();

            Type returnType = method.ReturnType;
            if (returnType == typeof(void))
            {
                Kind = DecoratorMethodKind.Void;
            }
            else if (returnType == typeof(Task))
            {
                Kind = DecoratorMethodKind.Task;
            }
            else if (returnType == typeof(ValueTask))
            {
                Kind = DecoratorMethodKind.ValueTask;
            }
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Kind = DecoratorMethodKind.TaskOfResult;
                ResultType = returnType.GetGenericArguments()[0];
            }
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                Kind = DecoratorMethodKind.ValueTaskOfResult;
                ResultType = returnType.GetGenericArguments()[0];
            }
            else
            {
                Kind = DecoratorMethodKind.Value;
                ResultType = returnType;
            }

            IsIntercepted = CanIntercept(method, Parameters, ResultType);
        }

        /// <summary>Gets the reflected method.</summary>
        public MethodInfo Method { get; }

        /// <summary>Gets the method's parameters.</summary>
        public ParameterInfo[] Parameters { get; }

        /// <summary>Gets the shape of the method's return, which selects how it is intercepted.</summary>
        public DecoratorMethodKind Kind { get; }

        /// <summary>
        /// Gets the type of value the method produces — its return type, or the <c>T</c> of a
        /// <c>Task&lt;T&gt;</c> / <c>ValueTask&lt;T&gt;</c>. Null when it produces none.
        /// </summary>
        public Type? ResultType { get; }

        /// <summary>
        /// Gets a value indicating whether handlers run for the method. False for methods with
        /// <c>ref</c>/<c>out</c>/<c>in</c> parameters or by-ref-like (<c>ref struct</c>) types, which cannot be
        /// captured by the interception delegate; those are forwarded directly.
        /// </summary>
        public bool IsIntercepted { get; }

        /// <summary>Gets a value indicating whether the method awaits its decorated call.</summary>
        public bool IsAsync => IsIntercepted && Kind != DecoratorMethodKind.Void && Kind != DecoratorMethodKind.Value;

        /// <summary>Gets the nullable-annotated name of <see cref="ResultType"/>; null when the method produces no value.</summary>
        public string? ResultTypeName => ResultType == null ? null : CSharpTypeName.OfResult(Method, ResultType);

        /// <inheritdoc />
        public override IEnumerable<Type> ReferencedTypes
        {
            get
            {
                yield return Method.ReturnType;
                foreach (ParameterInfo parameter in Parameters)
                {
                    yield return parameter.ParameterType;
                }
            }
        }

        /// <inheritdoc />
        public override string RenderedMember
        {
            get
            {
                string generics = Method.IsGenericMethodDefinition
                    ? "<" + string.Join(", ", Method.GetGenericArguments().Select(argument => argument.Name)) + ">"
                    : string.Empty;
                string async = IsAsync ? "async " : string.Empty;

                StringBuilder source = new StringBuilder();
                source.AppendLine($"{Indent}/// <inheritdoc />");
                source.AppendLine($"{Indent}{Modifier}{async}{CSharpTypeName.OfReturn(Method)} {Qualify(Method.Name)}{generics}({RenderParameters(Parameters)})");
                source.AppendLine($"{Indent}{{");
                source.AppendLine($"{Indent}    {RenderBody(generics)}");
                source.AppendLine($"{Indent}}}");
                return source.ToString().TrimEnd();
            }
        }

        private string RenderBody(string generics)
        {
            string call = $"{Target}.{Method.Name}{generics}({RenderArguments(Parameters)})";
            if (!IsIntercepted)
            {
                return Kind == DecoratorMethodKind.Void ? $"{call};" : $"return {call};";
            }

            string name = $"\"{Method.Name}\"";
            string args = Parameters.Length == 0
                ? "global::System.Array.Empty<object?>()"
                : $"new object?[] {{ {string.Join(", ", Parameters.Select(parameter => CSharpTypeName.Identifier(parameter.Name!)))} }}";

            switch (Kind)
            {
                case DecoratorMethodKind.Void:
                    return $"base.Intercept({name}, {args}, () => {call}).ThrowIfFailed();";
                case DecoratorMethodKind.Value:
                    return $"return base.Intercept<{ResultTypeName}>({name}, {args}, () => {call}).GetValue()!;";
                case DecoratorMethodKind.Task:
                    return $"(await base.InterceptAsync({name}, {args}, () => {call}).ConfigureAwait(false)).ThrowIfFailed();";
                case DecoratorMethodKind.ValueTask:
                    return $"(await base.InterceptAsync({name}, {args}, () => {call}.AsTask()).ConfigureAwait(false)).ThrowIfFailed();";
                case DecoratorMethodKind.TaskOfResult:
                    return $"return (await base.InterceptAsync<{ResultTypeName}>({name}, {args}, () => {call}).ConfigureAwait(false)).GetValue()!;";
                default:
                    return $"return (await base.InterceptAsync<{ResultTypeName}>({name}, {args}, () => {call}.AsTask()).ConfigureAwait(false)).GetValue()!;";
            }
        }

        private static bool CanIntercept(MethodInfo method, ParameterInfo[] parameters, Type? resultType)
        {
            if (parameters.Any(parameter => parameter.ParameterType.IsByRef || parameter.ParameterType.IsByRefLike || parameter.ParameterType.IsPointer))
            {
                return false;
            }

            Type returnType = method.ReturnType;
            if (returnType.IsByRefLike || returnType.IsPointer)
            {
                return false;
            }

            return resultType == null || (!resultType.IsByRefLike && !resultType.IsPointer);
        }
    }
}
