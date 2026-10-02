namespace Bam.Generators.Decorators
{
    /// <summary>
    /// File-system <see cref="IDecoratorTargetResolver"/>: writes <c>{ImplementationName}Decorator.cs</c> under
    /// the root directory, creating the directory if needed. Mirrors <c>FsServiceClientTargetResolver</c>.
    /// </summary>
    public class FsDecoratorTargetResolver : IDecoratorTargetResolver
    {
        /// <inheritdoc />
        public Stream GetTargetDecoratorStream(Func<string, Stream>? targetResolver, string rootDirectory, DecoratorModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (targetResolver != null)
            {
                return targetResolver(model.FileName);
            }

            Directory.CreateDirectory(rootDirectory);
            return new FileStream(Path.Combine(rootDirectory, model.FileName), FileMode.Create, FileAccess.Write);
        }
    }
}
