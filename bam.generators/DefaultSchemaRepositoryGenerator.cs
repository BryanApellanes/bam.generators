using Bam.Application;
using Bam.Data;
using Bam.Net;
using Bam.Net.Application;
using Bam.Net.Data.Repositories;
using Bam.Net.Data.Repositories.Handlebars;
using Bam.Net.Data.Schema;
using Bam.Net.Logging;
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
