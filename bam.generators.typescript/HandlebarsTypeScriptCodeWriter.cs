using Bam.Logging;

namespace Bam.Generators.TypeScript;

/// <summary>Renders a <see cref="TypeScriptContractModel"/> to a TypeScript module.</summary>
public interface ITypeScriptCodeWriter
{
    /// <summary>Renders the module for <paramref name="model"/>.</summary>
    /// <param name="model">The declarations.</param>
    /// <returns>The source, with <c>\n</c> line endings.</returns>
    string GetSource(TypeScriptContractModel model);
}

/// <summary>
/// Handlebars-based <see cref="ITypeScriptCodeWriter"/>, using the shared <see cref="Bam.Generators"/> Handlebars
/// infrastructure; mirrors <c>HandlebarsServiceClientCodeWriter</c>. A <c>TypeScriptContracts.hbs</c> in the override
/// directory wins over the embedded template. Line endings are normalised to <c>\n</c>, so the output doesn't depend on
/// how the template was checked out.
/// </summary>
public sealed class HandlebarsTypeScriptCodeWriter : Loggable, ITypeScriptCodeWriter
{
    /// <summary>The template's name.</summary>
    public const string TemplateName = "TypeScriptContracts";

    private bool _loaded;

    /// <summary>Initializes a writer using the embedded template plus a <c>./Templates</c> override directory.</summary>
    public HandlebarsTypeScriptCodeWriter()
        : this(new HandlebarsDirectory("./Templates"), new HandlebarsEmbeddedResources(typeof(HandlebarsTypeScriptCodeWriter).Assembly))
    {
    }

    /// <summary>Initializes a writer with the specified template sources.</summary>
    /// <param name="handlebarsDirectory">The override directory.</param>
    /// <param name="handlebarsEmbeddedResources">The embedded templates.</param>
    public HandlebarsTypeScriptCodeWriter(IHandlebarsDirectory handlebarsDirectory, IHandlebarsEmbeddedResources handlebarsEmbeddedResources)
    {
        HandlebarsDirectory = handlebarsDirectory;
        HandlebarsEmbeddedResources = handlebarsEmbeddedResources;
    }

    /// <summary>Gets the directory-based template source (override).</summary>
    public IHandlebarsDirectory HandlebarsDirectory { get; }

    /// <summary>Gets the embedded-resource template source (default).</summary>
    public IHandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; }

    /// <inheritdoc />
    public string GetSource(TypeScriptContractModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!_loaded)
        {
            HandlebarsDirectory.Reload();
            HandlebarsEmbeddedResources.Reload();
            _loaded = true;
        }
        string rendered;
        if (HandlebarsDirectory?.Templates?.ContainsKey(TemplateName) == true)
        {
            rendered = HandlebarsDirectory.Render(TemplateName, model);
        }
        else if (HandlebarsEmbeddedResources?.Templates?.ContainsKey(TemplateName) == true)
        {
            rendered = HandlebarsEmbeddedResources.Render(TemplateName, model);
        }
        else
        {
            throw new InvalidOperationException($"TypeScript template '{TemplateName}' not found.");
        }
        return rendered.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
