using Bam.Logging;

namespace Bam.Generators;

/// <summary>
/// Renders a <see cref="BamServiceClientModel"/> to C# source for a typed BAM service client.
/// </summary>
public interface IServiceClientCodeWriter
{
    /// <summary>Renders the client source for <paramref name="model"/>.</summary>
    string GetSource(BamServiceClientModel model);

    /// <summary>Renders the client source for <paramref name="model"/> and writes it to <paramref name="output"/>.</summary>
    void WriteClient(BamServiceClientModel model, Stream output);
}

/// <summary>
/// Handlebars-based <see cref="IServiceClientCodeWriter"/>. Renders embedded <c>.hbs</c> templates using the
/// shared <see cref="Bam.Generators"/> Handlebars infrastructure. Mirrors <c>HandlebarsCSharpDaoCodeWriter</c>.
/// </summary>
public class HandlebarsServiceClientCodeWriter : Loggable, IServiceClientCodeWriter
{
    /// <summary>Initializes a new instance using the executing assembly's embedded templates plus a <c>./Templates</c> override directory.</summary>
    public HandlebarsServiceClientCodeWriter()
        : this(new HandlebarsDirectory("./Templates"), new HandlebarsEmbeddedResources(typeof(HandlebarsServiceClientCodeWriter).Assembly))
    {
    }

    /// <summary>Initializes a new instance with the specified template sources.</summary>
    public HandlebarsServiceClientCodeWriter(IHandlebarsDirectory handlebarsDirectory, IHandlebarsEmbeddedResources handlebarsEmbeddedResources)
    {
        HandlebarsDirectory = handlebarsDirectory;
        HandlebarsEmbeddedResources = handlebarsEmbeddedResources;
    }

    /// <summary>Whether templates have been loaded.</summary>
    protected bool Loaded { get; set; }

    /// <summary>The directory-based template source (override).</summary>
    public IHandlebarsDirectory HandlebarsDirectory { get; set; }

    /// <summary>The embedded-resource template source (default).</summary>
    public IHandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }

    /// <summary>Loads templates if not already loaded.</summary>
    public void Load()
    {
        if (!Loaded)
        {
            Reload();
        }
    }

    /// <summary>Forces a reload of all template sources.</summary>
    public void Reload()
    {
        HandlebarsDirectory.Reload();
        HandlebarsEmbeddedResources.Reload();
        Loaded = true;
    }

    /// <inheritdoc />
    public string GetSource(BamServiceClientModel model)
    {
        Load();
        string templateName = model.Mode == GenerationMode.Interface ? "ServiceClientInterface" : "ServiceClientSubclass";
        return Render(templateName, model);
    }

    /// <inheritdoc />
    public void WriteClient(BamServiceClientModel model, Stream output)
    {
        string code = GetSource(model);
        StreamWriter writer = new StreamWriter(output, leaveOpen: true);
        writer.Write(code);
        writer.Flush();
    }

    private string Render(string templateName, object model)
    {
        if (HandlebarsDirectory?.Templates?.ContainsKey(templateName) == true)
        {
            return HandlebarsDirectory.Render(templateName, model);
        }
        if (HandlebarsEmbeddedResources?.Templates?.ContainsKey(templateName) == true)
        {
            return HandlebarsEmbeddedResources.Render(templateName, model);
        }
        throw new InvalidOperationException($"Service client template '{templateName}' not found.");
    }
}
