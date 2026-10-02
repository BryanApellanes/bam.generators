namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Resolves the output stream a generated decorator is written to.
    /// </summary>
    public interface IDecoratorTargetResolver
    {
        /// <summary>Gets the output stream for the decorator described by <paramref name="model"/>.</summary>
        /// <param name="targetResolver">Optional caller-supplied resolver mapping a file name to a stream.</param>
        /// <param name="rootDirectory">Root output directory used when <paramref name="targetResolver"/> is null.</param>
        /// <param name="model">The decorator being generated.</param>
        Stream GetTargetDecoratorStream(Func<string, Stream>? targetResolver, string rootDirectory, DecoratorModel model);
    }
}
