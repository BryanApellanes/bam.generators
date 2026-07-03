namespace Bam.Generators;

/// <summary>
/// Resolves the output stream a generated service client is written to.
/// </summary>
public interface IServiceClientTargetResolver
{
    /// <summary>Gets the output stream for the client generated from <paramref name="serviceType"/>.</summary>
    /// <param name="targetResolver">Optional caller-supplied resolver mapping a file name to a stream.</param>
    /// <param name="rootDirectory">Root output directory used when <paramref name="targetResolver"/> is null.</param>
    /// <param name="serviceType">The service type whose client is being generated.</param>
    Stream GetTargetClientStream(Func<string, Stream>? targetResolver, string rootDirectory, Type serviceType);
}

/// <summary>
/// File-system <see cref="IServiceClientTargetResolver"/>: writes <c>{ServiceName}Client.cs</c> under the root directory.
/// Mirrors <c>FsDaoTargetStreamResolver</c>.
/// </summary>
public class FsServiceClientTargetResolver : IServiceClientTargetResolver
{
    /// <inheritdoc />
    public Stream GetTargetClientStream(Func<string, Stream>? targetResolver, string rootDirectory, Type serviceType)
    {
        string fileName = $"{serviceType.Name}Client.cs";
        if (targetResolver != null)
        {
            return targetResolver(fileName);
        }

        Directory.CreateDirectory(rootDirectory);
        return new FileStream(Path.Combine(rootDirectory, fileName), FileMode.Create, FileAccess.Write);
    }
}
