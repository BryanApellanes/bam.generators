namespace Bam.Generators;

/// <summary>
/// Generates strongly-typed, multi-transport BAM service clients for <c>[WebService]</c> types by rendering
/// Handlebars templates. Mirrors the lifecycle of the DAO generators in <see cref="Bam.Generators"/>.
/// </summary>
public class BamServiceClientGenerator
{
    private readonly HashSet<ServiceClientRequest> _services = new();

    /// <summary>Initializes a new instance with the default Handlebars writer and file-system target resolver.</summary>
    public BamServiceClientGenerator()
        : this(new HandlebarsServiceClientCodeWriter(), new FsServiceClientTargetResolver())
    {
    }

    /// <summary>Initializes a new instance with the specified writer and target resolver.</summary>
    public BamServiceClientGenerator(IServiceClientCodeWriter codeWriter, IServiceClientTargetResolver targetResolver)
    {
        CodeWriter = codeWriter;
        TargetResolver = targetResolver;
    }

    /// <summary>The code writer used to render clients.</summary>
    public IServiceClientCodeWriter CodeWriter { get; set; }

    /// <summary>The resolver used to locate output streams.</summary>
    public IServiceClientTargetResolver TargetResolver { get; set; }

    /// <summary>Queues a service type for generation.</summary>
    public BamServiceClientGenerator AddServiceType(Type serviceType, GenerationMode mode = GenerationMode.Subclass, Type? interfaceType = null)
    {
        _services.Add(new ServiceClientRequest(serviceType, mode, interfaceType));
        return this;
    }

    /// <summary>Renders the client source for a single service type without writing it.</summary>
    public string GetSource(Type serviceType, GenerationMode mode = GenerationMode.Subclass, Type? interfaceType = null)
    {
        BamServiceClientModel model = new BamServiceClientModel(serviceType, mode, interfaceType);
        return CodeWriter.GetSource(model);
    }

    /// <summary>Writes the client source for every queued service type to <paramref name="outputDirectory"/>.</summary>
    public void WriteSource(string outputDirectory)
    {
        foreach (ServiceClientRequest request in _services)
        {
            BamServiceClientModel model = new BamServiceClientModel(request.ServiceType, request.Mode, request.InterfaceType);
            using Stream stream = TargetResolver.GetTargetClientStream(null, outputDirectory, request.ServiceType);
            CodeWriter.WriteClient(model, stream);
        }
    }

    private readonly record struct ServiceClientRequest(Type ServiceType, GenerationMode Mode, Type? InterfaceType);
}
