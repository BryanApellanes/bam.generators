// Types the compiler looks up by name that netstandard2.0 does not ship. Each is internal, so a consumer that
// has its own copy, or runs on a framework that has the real one, never sees two.
#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>Lets <c>init</c> accessors compile; the compiler emits it as a modreq on the setter.</summary>
    internal static class IsExternalInit
    {
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>Tells nullable analysis a parameter is not null when the method returns.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class NotNullAttribute : Attribute
    {
    }
}
#endif
