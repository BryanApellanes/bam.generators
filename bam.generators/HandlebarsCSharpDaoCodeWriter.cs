using Bam.Data.Schema;
using Bam.Logging;
using System.Reflection;

namespace Bam.Generators
{
    /// <summary>
    /// Writes DAO C# source code files by rendering Handlebars templates against schema and table models.
    /// Supports writing class, collection, columns, context, paged query, qi, query, and partial files.
    /// </summary>
    public class HandlebarsCSharpDaoCodeWriter : Loggable, IDaoCodeWriter
    {
        /// <summary>
        /// Initializes a new instance using a default <see cref="FsDaoTargetStreamResolver"/>.
        /// </summary>
        public HandlebarsCSharpDaoCodeWriter()
            : this(new FsDaoTargetStreamResolver())
        {
        }

        /// <summary>
        /// Initializes a new instance using the specified target stream resolver, with default template sources.
        /// </summary>
        /// <param name="daoTargetStreamResolver">The resolver that determines output stream locations for generated files.</param>
        public HandlebarsCSharpDaoCodeWriter(IDaoTargetStreamResolver? daoTargetStreamResolver)
            : this(new HandlebarsDirectory("./Templates"), new HandlebarsEmbeddedResources(Assembly.GetExecutingAssembly()), daoTargetStreamResolver)
        {
        }

        /// <summary>
        /// Initializes a new instance with the specified template sources and target stream resolver.
        /// </summary>
        /// <param name="handlebarsDirectory">The directory-based Handlebars template source.</param>
        /// <param name="handlebarsEmbeddedResources">The embedded resource-based Handlebars template source.</param>
        /// <param name="daoTargetStreamResolver">The resolver that determines output stream locations for generated files.</param>
        public HandlebarsCSharpDaoCodeWriter(IHandlebarsDirectory handlebarsDirectory, IHandlebarsEmbeddedResources handlebarsEmbeddedResources, IDaoTargetStreamResolver? daoTargetStreamResolver = null)
        {
            DaoTargetStreamResolver = daoTargetStreamResolver ?? new FsDaoTargetStreamResolver();
            HandlebarsDirectory = handlebarsDirectory;
            HandlebarsEmbeddedResources = handlebarsEmbeddedResources;
        }

        /// <summary>
        /// Gets or sets a value indicating whether templates have been loaded.
        /// </summary>
        protected bool Loaded { get; set; }

        /// <summary>
        /// Loads templates if they have not already been loaded.
        /// </summary>
        public void Load()
        {
            if (!Loaded)
            {
                Reload();
            }
        }

        /// <summary>
        /// Forces a reload of all Handlebars templates from both directory and embedded resource sources.
        /// </summary>
        public void Reload()
        {
            HandlebarsDirectory.Reload();
            HandlebarsEmbeddedResources.Reload();
            Loaded = true;
        }

        /// <summary>
        /// Gets or sets the target namespace for generated DAO code.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Gets or sets the resolver that determines output stream locations for each generated file type.
        /// </summary>
        public IDaoTargetStreamResolver DaoTargetStreamResolver { get; set; }

        /// <summary>
        /// Gets or sets the directory-based Handlebars template source.
        /// </summary>
        public IHandlebarsDirectory HandlebarsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the embedded resource-based Handlebars template source.
        /// </summary>
        public IHandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }

        /// <summary>
        /// Writes the DAO class file for the specified table using the "Class" Handlebars template.
        /// </summary>
        /// <param name="schema">The schema definition containing the table.</param>
        /// <param name="targetResolver">A function that resolves a file name to an output stream.</param>
        /// <param name="rootDirectory">The root directory for generated output files.</param>
        /// <param name="table">The table to generate the DAO class for.</param>
        public void WriteDaoClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, ITable table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("Class", renderModel, DaoTargetStreamResolver.GetTargetClassStream(targetResolver, rootDirectory, table));
        }

        /// <summary>
        /// Writes the collection class file for the specified table using the "Collection" Handlebars template.
        /// </summary>
        /// <param name="schema">The schema definition containing the table.</param>
        /// <param name="targetResolver">A function that resolves a file name to an output stream.</param>
        /// <param name="rootDirectory">The root directory for generated output files.</param>
        /// <param name="table">The table to generate the collection class for.</param>
        public void WriteCollectionClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, ITable table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("Collection", renderModel, DaoTargetStreamResolver.GetTargetCollectionStream(targetResolver, rootDirectory, table));
        }

        /// <summary>
        /// Writes the columns class file for the specified table using the "ColumnsClass" Handlebars template.
        /// </summary>
        /// <param name="schema">The schema definition containing the table.</param>
        /// <param name="targetResolver">A function that resolves a file name to an output stream.</param>
        /// <param name="rootDirectory">The root directory for generated output files.</param>
        /// <param name="table">The table to generate the columns class for.</param>
        public void WriteColumnsClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, ITable table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("ColumnsClass", renderModel, DaoTargetStreamResolver.GetTargetColumnsClassStream(targetResolver, rootDirectory, table));
        }

        /// <summary>
        /// Writes the database context class file for the schema using the "Context" Handlebars template.
        /// </summary>
        /// <param name="schema">The schema definition to generate the context class for.</param>
        /// <param name="targetResolver">A function that resolves a file name to an output stream.</param>
        /// <param name="rootDirectory">The root directory for generated output files.</param>
        public void WriteContextClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory)
        {
            Load();
            DaoContextModel renderModel = GetContextModel(schema);
            Render("Context", renderModel, DaoTargetStreamResolver.GetTargetContextStream(targetResolver, rootDirectory, schema));
        }

        /// <summary>
        /// Writes the paged query class file for the specified table using the "PagedQueryClass" Handlebars template.
        /// </summary>
        /// <param name="schema">The schema definition containing the table.</param>
        /// <param name="targetResolver">A function that resolves a file name to an output stream.</param>
        /// <param name="rootDirectory">The root directory for generated output files.</param>
        /// <param name="table">The table to generate the paged query class for.</param>
        public void WritePagedQueryClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, ITable table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("PagedQueryClass", renderModel, DaoTargetStreamResolver.GetTargetPagedQueryClassStream(targetResolver, rootDirectory, table));
        }

        /// <summary>
        /// Writes the query item (Qi) class file for the specified table using the "QiClass" Handlebars template.
        /// </summary>
        /// <param name="schema">The schema definition containing the table.</param>
        /// <param name="targetResolver">A function that resolves a file name to an output stream.</param>
        /// <param name="rootDirectory">The root directory for generated output files.</param>
        /// <param name="table">The table to generate the Qi class for.</param>
        public void WriteQiClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, ITable table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("QiClass", renderModel, DaoTargetStreamResolver.GetTargetQiClassStream(targetResolver, rootDirectory, table));
        }

        /// <summary>
        /// Writes the query class file for the specified table using the "QueryClass" Handlebars template.
        /// </summary>
        /// <param name="schema">The schema definition containing the table.</param>
        /// <param name="targetResolver">A function that resolves a file name to an output stream.</param>
        /// <param name="rootDirectory">The root directory for generated output files.</param>
        /// <param name="table">The table to generate the query class for.</param>
        public void WriteQueryClass(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string rootDirectory, ITable table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("QueryClass", renderModel, DaoTargetStreamResolver.GetTargetQueryClassStream(targetResolver, rootDirectory, table));
        }

        /// <summary>
        /// Writes a partial class file for the specified table using the "Partial" Handlebars template.
        /// </summary>
        /// <param name="schema">The schema definition containing the table.</param>
        /// <param name="targetResolver">A function that resolves a file name to an output stream.</param>
        /// <param name="root">The root directory for generated output files.</param>
        /// <param name="table">The table to generate the partial class for.</param>
        public void WritePartial(IDaoSchemaDefinition schema, Func<string, Stream> targetResolver, string root, ITable table)
        {
            Load();
            DaoTableSchemaModel renderModel = GetModel(schema, table);
            Render("Partial", renderModel, DaoTargetStreamResolver.GetTargetPartialClassStream(targetResolver, root, table));
        }

        private DaoContextModel GetContextModel(IDaoSchemaDefinition schema)
        {
            return new DaoContextModel { Model = schema, Namespace = Namespace };
        }

        private DaoTableSchemaModel GetModel(IDaoSchemaDefinition schema, ITable table)
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
