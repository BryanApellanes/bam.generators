using Bam.Data.Repositories;
using System.Reflection;

namespace Bam.Generators
{
    /// <summary>
    /// Generates wrapper classes that bridge POCO types to their DAO counterparts, using Handlebars
    /// templates to produce C# source code and Roslyn to compile the result into an assembly.
    /// </summary>
    public class HandlebarsWrapperGenerator : TemplatedWrapperGenerator
    {
        /// <summary>
        /// Initializes a new instance with the specified schema provider, using default template directories and embedded resources.
        /// </summary>
        /// <param name="schemaProvider">The schema provider used to resolve type schemas.</param>
        public HandlebarsWrapperGenerator(ISchemaProvider schemaProvider) : base(schemaProvider, new HandlebarsTemplateRenderer<WrapperModel>())
        {
            HandlebarsDirectory = new HandlebarsDirectory("./Templates");
            HandlebarsEmbeddedResources = new HandlebarsEmbeddedResources(GetType().Assembly);
        }

        /// <summary>
        /// Gets or sets the directory-based Handlebars template source for wrapper templates.
        /// </summary>
        public HandlebarsDirectory HandlebarsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the embedded resource-based Handlebars template source for wrapper templates.
        /// </summary>
        public HandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }

        object _generateLock = new object();
        /// <summary>
        /// Compiles the previously written wrapper source files into an assembly using Roslyn.
        /// This method is thread-safe.
        /// </summary>
        /// <returns>A <see cref="GeneratedAssemblyInfo"/> containing the compiled wrapper assembly.</returns>
        public override GeneratedAssemblyInfo GenerateAssembly()
        {
            lock (_generateLock)
            {
                RoslynCompiler compiler = new RoslynCompiler();
                Assembly assembly = compiler.CompileDirectoriesToAssembly($"{WrapperNamespace}.Wrapper.dll", new DirectoryInfo(WriteSourceTo));
                GeneratedAssemblyInfo result = new GeneratedAssemblyInfo($"{WrapperNamespace}.Wrapper.dll", assembly);
                result.Save();
                return result;
            }
        }

        /// <summary>
        /// Generates wrapper C# source files for each type in the schema using the "Wrapper" Handlebars template,
        /// writing one file per type to the specified directory.
        /// </summary>
        /// <param name="writeSourceDir">The directory to write the generated wrapper source files to.</param>
        public override void WriteSource(string writeSourceDir)
        {
            WriteSourceTo = writeSourceDir;
            foreach (Type type in TypeSchema.Tables)
            {
                HandlebarsWrapperModel model = new HandlebarsWrapperModel(type, TypeSchema, WrapperNamespace, DaoNamespace);
                model.TemplateRenderer = new HandlebarsTemplateRenderer(HandlebarsEmbeddedResources, HandlebarsDirectory);
                string fileName = $"{type.Name.TrimNonLetters()}Wrapper.cs";
                using (StreamWriter sw = new StreamWriter(Path.Combine(writeSourceDir, fileName)))
                {
                    sw.Write(model.Render());
                }
            }
        }
    }
}
