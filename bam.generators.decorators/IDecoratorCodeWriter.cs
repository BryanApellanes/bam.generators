namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Renders a <see cref="DecoratorModel"/> to C# source for a typed decorator and its extension methods.
    /// </summary>
    public interface IDecoratorCodeWriter
    {
        /// <summary>Renders the decorator source for <paramref name="model"/>.</summary>
        string GetSource(DecoratorModel model);

        /// <summary>Renders the decorator source for <paramref name="model"/> and writes it to <paramref name="output"/>.</summary>
        void WriteDecorator(DecoratorModel model, Stream output);
    }
}
