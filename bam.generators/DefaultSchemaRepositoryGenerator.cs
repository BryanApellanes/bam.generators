using Bam.Application;
using Bam.Data;
using Bam;
using Bam.Application;
using Bam.Data.Repositories;
using Bam.Data.Schema;
using Bam.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Bam.Generators
{
    public class DefaultSchemaRepositoryGenerator : SchemaRepositoryGenerator
    {
        public DefaultSchemaRepositoryGenerator(ILogger? logger = null) : this(DaoRepoGenerationConfig.LoadDefault(), logger)
        { }

        public DefaultSchemaRepositoryGenerator(IDaoRepoGenerationConfig config, ILogger? logger = null) : base(new DefaultSchemaRepositoryGeneratorSettings(config), logger)
        {
            TemplateRenderer = new HandlebarsTemplateRenderer(new HandlebarsEmbeddedResources(typeof(DefaultSchemaRepositoryGenerator).Assembly), new HandlebarsDirectory(config.TemplatePath));

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
