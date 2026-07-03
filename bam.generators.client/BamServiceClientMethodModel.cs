using System.Reflection;
using System.Text;

namespace Bam.Generators;

/// <summary>
/// Handlebars render model for one remotable method on a <c>[WebService]</c> type. Precomputes the
/// asynchronous and synchronous client members (Approach A: the generated client inlines a private
/// <c>InvokeRemoteAsync</c> helper and each member is a thin delegation to it).
/// </summary>
public class BamServiceClientMethodModel
{
    /// <summary>Initializes the model by reflecting over <paramref name="method"/>.</summary>
    public BamServiceClientMethodModel(Type serviceType, MethodInfo method, GenerationMode mode)
    {
        ServiceType = serviceType;
        Method = method;
        Mode = mode;
        MethodName = method.Name;
        IsVirtual = method.IsVirtual && !method.IsFinal;
        Parameters = method.GetParameters();

        Type returnType = method.ReturnType;
        ReturnsTask = typeof(Task).IsAssignableFrom(returnType);
        if (ReturnsTask && returnType.IsGenericType)
        {
            ResultType = returnType.GetGenericArguments()[0]; // Task<X> -> X
        }
        else if (ReturnsTask || returnType == typeof(void))
        {
            ResultType = null; // Task or void -> no result value
        }
        else
        {
            ResultType = returnType; // synchronous X
        }
    }

    /// <summary>The service type declaring the method.</summary>
    public Type ServiceType { get; }
    /// <summary>The reflected method.</summary>
    public MethodInfo Method { get; }
    /// <summary>The generation mode (controls the override/impl modifier).</summary>
    public GenerationMode Mode { get; }
    /// <summary>The method name.</summary>
    public string MethodName { get; }
    /// <summary>Whether the method is overridable (virtual and not sealed).</summary>
    public bool IsVirtual { get; }
    /// <summary>Whether the method returns a <see cref="Task"/> or <c>Task&lt;T&gt;</c>.</summary>
    public bool ReturnsTask { get; }
    /// <summary>The result type (T of a sync method or of <c>Task&lt;T&gt;</c>); null for void / non-generic Task.</summary>
    public Type? ResultType { get; }
    /// <summary>The method parameters.</summary>
    public ParameterInfo[] Parameters { get; }

    private string ParamSignature =>
        string.Join(", ", Parameters.Select(p => $"{CSharpTypeName.Of(p.ParameterType)} {p.Name}"));

    private string ParamNames => string.Join(", ", Parameters.Select(p => p.Name));

    private string ArgArray => Parameters.Length == 0
        ? "global::System.Array.Empty<object>()"
        : $"new object[] {{ {ParamNames} }}";

    private string AsyncName => ReturnsTask ? MethodName : MethodName + "Async";

    private string SyncName
    {
        get
        {
            if (!ReturnsTask)
            {
                return MethodName;
            }
            return MethodName.EndsWith("Async", StringComparison.Ordinal) && MethodName.Length > "Async".Length
                ? MethodName.Substring(0, MethodName.Length - "Async".Length)
                : MethodName + "Sync";
        }
    }

    // The member matching the base/interface method takes the mode-appropriate modifier;
    // the complementary member is a plain added public method.
    private string MatchModifier => Mode == GenerationMode.Subclass ? "override " : string.Empty;

    /// <summary>The fully rendered async + sync members for this method (consumed by the method template).</summary>
    public string RenderedMembers
    {
        get
        {
            string asyncMod = ReturnsTask ? MatchModifier : string.Empty; // async member matches the base iff base is async
            string syncMod = ReturnsTask ? string.Empty : MatchModifier;  // sync member matches the base iff base is sync
            string sig = ParamSignature;
            string names = ParamNames;
            string args = ArgArray;

            StringBuilder sb = new StringBuilder();

            // ---- async member ----
            if (ResultType == null)
            {
                sb.AppendLine($"        public {asyncMod}async global::System.Threading.Tasks.Task {AsyncName}({sig})");
                sb.AppendLine("        {");
                sb.AppendLine($"            await InvokeRemoteAsync<object>(\"{MethodName}\", {args}).ConfigureAwait(false);");
                sb.AppendLine("        }");
            }
            else
            {
                string tn = CSharpTypeName.Of(ResultType);
                sb.AppendLine($"        public {asyncMod}async global::System.Threading.Tasks.Task<{tn}> {AsyncName}({sig})");
                sb.AppendLine("        {");
                sb.AppendLine($"            return await InvokeRemoteAsync<{tn}>(\"{MethodName}\", {args}).ConfigureAwait(false);");
                sb.AppendLine("        }");
            }

            sb.AppendLine();

            // ---- sync member ----
            if (ResultType == null)
            {
                sb.AppendLine($"        public {syncMod}void {SyncName}({sig})");
                sb.AppendLine("        {");
                sb.AppendLine($"            {AsyncName}({names}).GetAwaiter().GetResult();");
                sb.AppendLine("        }");
            }
            else
            {
                string tn = CSharpTypeName.Of(ResultType);
                sb.AppendLine($"        public {syncMod}{tn} {SyncName}({sig})");
                sb.AppendLine("        {");
                sb.AppendLine($"            return {AsyncName}({names}).GetAwaiter().GetResult();");
                sb.AppendLine("        }");
            }

            return sb.ToString();
        }
    }
}
