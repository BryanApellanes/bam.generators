using Bam.Data;
using Bam.Data.Repositories;
using Bam.Logging;

namespace Bam.Generators
{
    /// <summary>
    /// A schema repository generator that uses Handlebars templates to produce DAO source code
    /// from a <see cref="IDaoRepoGenerationConfig"/>.
    /// </summary>
    public class HandlebarsSchemaRepositoryGenerator : SchemaRepositoryGenerator
    {
        /// <summary>
        /// Initializes a new instance using configuration loaded from the default file (DaoRepoGenerationConfig.yaml).
        /// </summary>
        /// <param name="logger">An optional logger instance.</param>
        public HandlebarsSchemaRepositoryGenerator(ILogger? logger = null) : this(DaoRepoGenerationConfig.ReadFile(), logger)
        { }

        /// <summary>
        /// Initializes a new instance using the specified generation configuration.
        /// </summary>
        /// <param name="config">The DAO repository generation configuration.</param>
        /// <param name="logger">An optional logger instance.</param>
        public HandlebarsSchemaRepositoryGenerator(IDaoRepoGenerationConfig config, ILogger? logger = null) : base(new HandlebarsSchemaRepositoryGeneratorSettings(config), logger)
        {
            TemplateRenderer = new HandlebarsTemplateRenderer(new HandlebarsEmbeddedResources(typeof(HandlebarsSchemaRepositoryGenerator).Assembly), new HandlebarsDirectory(config.TemplatePath));

            Configure(config);
            Handlebars.HandlebarsDirectory = new HandlebarsDirectory(config.TemplatePath);
            Handlebars.HandlebarsEmbeddedResources = new HandlebarsEmbeddedResources(typeof(SchemaRepositoryGenerator).Assembly);
        }

        /// <summary>
        /// Creates a <see cref="HandlebarsSchemaTypeModel"/> for the specified type, which includes
        /// Handlebars-specific properties such as pluralized type names.
        /// </summary>
        /// <param name="t">The CLR type to create a schema type model for.</param>
        /// <returns>A <see cref="HandlebarsSchemaTypeModel"/> representing the type.</returns>
        public override SchemaTypeModel GetSchemaTypeModel(Type t)
        {
            return HandlebarsSchemaTypeModel.FromType(t, DaoNamespace);
        }
    }
}
