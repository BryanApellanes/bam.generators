namespace Bam.Generators.TypeScript;

/// <summary>Renders a <see cref="TypeScriptContractModel"/> to a TypeScript module.</summary>
public interface ITypeScriptCodeWriter
{
    /// <summary>Renders the module for <paramref name="model"/>.</summary>
    /// <param name="model">The declarations.</param>
    /// <returns>The source, with <c>\n</c> line endings.</returns>
    string GetSource(TypeScriptContractModel model);
}
