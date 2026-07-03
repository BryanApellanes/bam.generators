namespace Bam.Generators;

/// <summary>
/// Thrown when a service client cannot be generated for a type — e.g. the type is not a <c>[WebService]</c>,
/// or Subclass mode was requested for a type with non-virtual remotable methods.
/// </summary>
public class ServiceClientGenerationException : Exception
{
    /// <summary>Initializes a new instance for the specified service type and reason.</summary>
    public ServiceClientGenerationException(Type serviceType, string message)
        : base($"[{serviceType.FullName}] {message}")
    {
        ServiceType = serviceType;
    }

    /// <summary>Gets the service type that could not be generated.</summary>
    public Type ServiceType { get; }
}
