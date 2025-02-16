using Bam.Data;
using Bam.Data.Repositories;
using Bam.Logging;

namespace Bam.Generators
{
    public class HandlebarsSchemaRepositoryGenerator : SchemaRepositoryGenerator
    {
        public HandlebarsSchemaRepositoryGenerator(ILogger? logger = null) : this(DaoRepoGenerationConfig.ReadFile(), logger)
        { }

        public HandlebarsSchemaRepositoryGenerator(IDaoRepoGenerationConfig config, ILogger? logger = null) : base(new HandlebarsSchemaRepositoryGeneratorSettings(config), logger)
        {
            TemplateRenderer = new HandlebarsTemplateRenderer(new HandlebarsEmbeddedResources(typeof(HandlebarsSchemaRepositoryGenerator).Assembly), new HandlebarsDirectory(config.TemplatePath));

            Configure(config);
            Handlebars.HandlebarsDirectory = new HandlebarsDirectory(config.TemplatePath);
            Handlebars.HandlebarsEmbeddedResources = new HandlebarsEmbeddedResources(typeof(SchemaRepositoryGenerator).Assembly);
        }

        public override SchemaTypeModel GetSchemaTypeModel(Type t)
        {
            return HandlebarsSchemaTypeModel.FromType(t, DaoNamespace);
        }
    }
}
