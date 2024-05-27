using Bam;
using Bam.Data.Schema;
using Bam.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Bam.Generators
{
    public class HandlebarsCSharpDaoCodeWriter : Loggable, IDaoCodeWriter
    {
        public HandlebarsCSharpDaoCodeWriter()
            : this(new FsDaoTargetStreamResolver())
        {
        }

        public HandlebarsCSharpDaoCodeWriter(IDaoTargetStreamResolver? daoTargetStreamResolver)
            : this(new HandlebarsDirectory("./Templates"), new HandlebarsEmbeddedResources(Assembly.GetExecutingAssembly()), daoTargetStreamResolver)
        {
        }

        public HandlebarsCSharpDaoCodeWriter(IHandlebarsDirectory handlebarsDirectory, IHandlebarsEmbeddedResources handlebarsEmbeddedResources, IDaoTargetStreamResolver? daoTargetStreamResolver = null)
        {
            DaoTargetStreamResolver = daoTargetStreamResolver ?? new FsDaoTargetStreamResolver();
            HandlebarsDirectory = handlebarsDirectory;
            HandlebarsEmbeddedResources = handlebarsEmbeddedResources;
        }

        protected bool Loaded { get; set; }

        public void Load()
        {
            if (!Loaded)
            {
                Reload();
            }
        }

        public void Reload()
        {
            HandlebarsDirectory.Reload();
            HandlebarsEmbeddedResources.Reload();
            Loaded = true;
        }

        public string Namespace { get; set; }
        public IDaoTargetStreamResolver DaoTargetStreamResolver { get; set; }
        public IHandlebarsDirectory HandlebarsDirectory { get; set; }
        public IHandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }

        public void WriteDaoClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, Table table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("Class", renderModel, DaoTargetStreamResolver.GetTargetClassStream(targetResolver, rootDirectory, table));
        }

        public void WriteCollectionClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, Table table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("Collection", renderModel, DaoTargetStreamResolver.GetTargetCollectionStream(targetResolver, rootDirectory, table));
        }

        public void WriteColumnsClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, Table table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("ColumnsClass", renderModel, DaoTargetStreamResolver.GetTargetColumnsClassStream(targetResolver, rootDirectory, table));
        }

        public void WriteContextClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory)
        {
            Load();
            DaoContextModel renderModel = GetContextModel(schema);
            Render("Context", renderModel, DaoTargetStreamResolver.GetTargetContextStream(targetResolver, rootDirectory, schema));
        }

        public void WritePagedQueryClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, Table table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("PagedQueryClass", renderModel, DaoTargetStreamResolver.GetTargetPagedQueryClassStream(targetResolver, rootDirectory, table));
        }

        public void WriteQiClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, Table table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("QiClass", renderModel, DaoTargetStreamResolver.GetTargetQiClassStream(targetResolver, rootDirectory, table));
        }

        public void WriteQueryClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, Table table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("QueryClass", renderModel, DaoTargetStreamResolver.GetTargetQueryClassStream(targetResolver, rootDirectory, table));
        }

        public void WritePartial(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string root, Table table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("Partial", renderModel, DaoTargetStreamResolver.GetTargetPartialClassStream(targetResolver, root, table));
        }

        private DaoContextModel GetContextModel(IDaoSchemaDefinition schema)
        {
            return new DaoContextModel { Model = schema, Namespace = Namespace };
        }

        private DaoTableSchemaModel GetModel(IDaoSchemaDefinition schema, Table table)
        {
            return new DaoTableSchemaModel { Model = table, Schema = schema, Namespace = Namespace };
        }

        private void Render(string templateName, object renderModel, Stream output)
        {
            if ((HandlebarsDirectory?.Templates?.ContainsKey(templateName)).Value)
            {
                string code = HandlebarsDirectory.Render(templateName, renderModel);

                code.WriteToStream(output);
            }
            else if ((HandlebarsEmbeddedResources?.Templates?.ContainsKey(templateName)).Value)
            {
                string code = HandlebarsEmbeddedResources.Render(templateName, renderModel);
                code.WriteToStream(output);
            }
        }
    }
}
