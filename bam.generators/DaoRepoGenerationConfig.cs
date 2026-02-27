using System.Reflection;
using Bam.Data;
using Bam.Data.Repositories;

namespace Bam.Generators
{
    /// <summary>
    /// Configuration for generating a dao repository.
    /// </summary>
    public class DaoRepoGenerationConfig : IDaoRepoGenerationConfig
    {
        /// <summary>
        /// Initializes a new instance with default settings: CheckForIds enabled, template path under AppPaths.Data,
        /// output to "Generated_Dao", and TypeAssembly set to the entry assembly.
        /// </summary>
        public DaoRepoGenerationConfig()
        {
            CheckForIds = true;
            TemplatePath = Path.Combine(AppPaths.Data, "Templates");
            WriteSourceTo = "Generated_Dao";
            Assembly? entryAssembly =  Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                TypeAssembly = entryAssembly.GetFileInfo().FullName;
            }
        }

        /// <summary>
        /// The default file path for the YAML configuration file ("./DaoRepoGenerationConfig.yaml").
        /// </summary>
        public static string DefaultFilePath = $"./{nameof(DaoRepoGenerationConfig)}.yaml";
        
        /// <summary>
        /// Gets or sets the path to the templates.
        /// </summary>
        public string TemplatePath { get; set; }

        /// <summary>
        /// Gets or sets the type assembly.
        /// </summary>
        [CompositeKey]
        public string TypeAssembly { get; set; } = null!;

        /// <summary>
        /// Gets or sets the schema name.
        /// </summary>
        [CompositeKey]
        public string SchemaName { get; set; } = null!;

        /// <summary>
        /// Gets or sets from which namespace to find types to generate dao types and wrappers for.
        /// </summary>
        /// <value>
        /// From name space.
        /// </value>
        [CompositeKey]
        public string FromNamespace { get; set; } = null!;

        /// <summary>
        /// Gets or sets the target namespace for generated dao types.  Defaults to FromNamespace suffixed with ".Dao".
        /// </summary>
        [CompositeKey]
        public string ToNamespace
        {
            get => _toNamespace ?? $"{FromNamespace}.Dao";
            set => _toNamespace = value;
        }
        private string? _toNamespace;

        /// <summary>
        /// Gets or sets the file system path to write source code to.
        /// </summary>
        public string WriteSourceTo { get; set; }

        /// <summary>
        /// Check the specified data classes for Id properties
        /// </summary>
        public bool CheckForIds { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to generate a repository that extends AsyncDaoRepository instead of DaoRepository.
        /// UseInheritanceSchema takes precedence if both are true.
        /// </summary>
        public bool UseAsync { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to generate a repository that inherits from `DaoInheritanceRepository`, if `false` inherit from DaoRepository.
        /// </summary>
        public bool UseInheritanceSchema { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether compiler warnings should be treated as errors during code generation.
        /// </summary>
        public bool WarningsAsErrors { get; set; }
        
        /// <summary>
        /// Load the config from the file ./DaoRepoGenerationConfig.yaml.
        /// </summary>
        /// <returns>A <see cref="DaoRepoGenerationConfig"/> deserialized from the default YAML file.</returns>
        public static DaoRepoGenerationConfig ReadFile()
        {
            return ReadFrom(DefaultFilePath);
        }

        /// <summary>
        /// Load the configuration from the specified file path.
        /// </summary>
        /// <param name="path">The file path to load the configuration from.</param>
        /// <returns>A <see cref="DaoRepoGenerationConfig"/> deserialized from the specified file.</returns>
        public static DaoRepoGenerationConfig ReadFrom(string path)
        {
            return ReadFrom(new FileInfo(path));
        }

        /// <summary>
        /// Load the configuration from the specified file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns>DaoRepoGenerationConfig</returns>
        public static DaoRepoGenerationConfig ReadFrom(FileInfo file)
        {
            return file.FromFile<DaoRepoGenerationConfig>();
        }

    }
}
