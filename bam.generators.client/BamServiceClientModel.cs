using System.Reflection;
using Bam.Server;

namespace Bam.Generators;

/// <summary>
/// Handlebars render model for a generated service client. Reflects a <c>[WebService]</c> type into the
/// data the client templates consume, validating the type per the chosen <see cref="GenerationMode"/>.
/// </summary>
public class BamServiceClientModel
{
    /// <summary>Builds the model for <paramref name="serviceType"/>, validating it for <paramref name="mode"/>.</summary>
    /// <exception cref="ServiceClientGenerationException">
    /// Thrown when the type is not a <c>[WebService]</c>, or Subclass mode is requested but some remotable
    /// method is not virtual.
    /// </exception>
    public BamServiceClientModel(Type serviceType, GenerationMode mode = GenerationMode.Subclass, Type? interfaceType = null)
    {
        if (!Attribute.IsDefined(serviceType, typeof(WebServiceAttribute)))
        {
            throw new ServiceClientGenerationException(serviceType, "Type is not adorned with [WebService].");
        }

        ServiceType = serviceType;
        Mode = mode;
        InterfaceType = interfaceType;

        MethodInfo[] methods = serviceType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !m.IsGenericMethodDefinition && m.DeclaringType != typeof(object))
            .ToArray();

        if (mode == GenerationMode.Subclass)
        {
            string[] nonVirtual = methods
                .Where(m => !(m.IsVirtual && !m.IsFinal))
                .Select(m => m.Name)
                .ToArray();
            if (nonVirtual.Length > 0)
            {
                throw new ServiceClientGenerationException(serviceType,
                    $"Subclass mode requires virtual remotable methods. Non-virtual: {string.Join(", ", nonVirtual)}. " +
                    "Make them virtual or use GenerationMode.Interface.");
            }
        }

        Methods = methods.Select(m => new BamServiceClientMethodModel(serviceType, m, mode)).ToList();
    }

    /// <summary>The service type being wrapped.</summary>
    public Type ServiceType { get; }
    /// <summary>The generation mode.</summary>
    public GenerationMode Mode { get; }
    /// <summary>The interface to implement in Interface mode, if explicitly supplied.</summary>
    public Type? InterfaceType { get; }
    /// <summary>The remotable methods reflected from the service type.</summary>
    public List<BamServiceClientMethodModel> Methods { get; }

    /// <summary>The namespace of the generated client.</summary>
    public string Namespace => (ServiceType.Namespace ?? "Bam.Generated") + ".Clients";
    /// <summary>The fully-qualified name of the service type (used in <c>typeof(...)</c>).</summary>
    public string ServiceFullName => CSharpTypeName.Of(ServiceType);
    /// <summary>The generated client's simple type name.</summary>
    public string ClientTypeName => ServiceType.Name + "Client";
    /// <summary>The fully-qualified base type for Subclass mode.</summary>
    public string BaseTypeName => CSharpTypeName.Of(ServiceType);
    /// <summary>The fully-qualified interface for Interface mode (supplied, or the <c>I{Name}</c> convention).</summary>
    public string InterfaceName => InterfaceType != null
        ? CSharpTypeName.Of(InterfaceType)
        : $"global::{(ServiceType.Namespace == null ? string.Empty : ServiceType.Namespace + ".")}I{ServiceType.Name}";
}
