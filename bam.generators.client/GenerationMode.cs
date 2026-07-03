namespace Bam.Generators;

/// <summary>
/// Selects the shape of a generated BAM service client.
/// </summary>
public enum GenerationMode
{
    /// <summary>
    /// Generate a client that subclasses the service type and overrides its remotable methods.
    /// Requires every remotable method to be <see langword="virtual"/>; generation fails otherwise.
    /// </summary>
    Subclass,

    /// <summary>
    /// Generate a client that implements a supplied or convention (<c>I{ServiceName}</c>) interface for the service.
    /// </summary>
    Interface
}
