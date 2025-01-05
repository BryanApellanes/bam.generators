using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Bam.Data;
using Bam;
using Bam.Configuration;
using Bam.Data.Repositories;

namespace Bam.Generators
{
    /// <summary>
    /// Configuration for generating a dao repository.
    /// </summary>
    public class DaoRepoGenerationConfig : IDaoRepoGenerationConfig
    {
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

        public static string DefaultFilePath = $"./{nameof(DaoRepoGenerationConfig)}.yaml";
        
        /// <summary>
        /// Gets or sets the path to the templates.
        /// </summary>
        public string TemplatePath { get; set; }

        /// <summary>
        /// Gets or sets the type assembly.
        /// </summary>
        [CompositeKey]
        public string TypeAssembly { get; set; }

        /// <summary>
        /// Gets or sets the schema name.
        /// </summary>
        [CompositeKey]
        public string SchemaName { get; set; }

        /// <summary>
        /// Gets or sets from which namespace to find types to generate dao types and wrappers for.
        /// </summary>
        /// <value>
        /// From name space.
        /// </value>
        [CompositeKey]
        public string FromNamespace { get; set; }

        /// <summary>
        /// Gets or sets the file system path to write source code to.
        /// </summary>
        public string WriteSourceTo { get; set; }

        /// <summary>
        /// Check the specified data classes for Id properties
        /// </summary>
        public bool CheckForIds { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to generate a repository that inherits from `DaoInheritanceRepository`, if `false` inherit from DaoRepository.
        /// </summary>
        public bool UseInheritanceSchema { get; set; }

        /// <summary>
        /// Load the config from the file ./DaoRepoGenerationConfig.yaml.
        /// </summary>
        /// <returns></returns>
        public static DaoRepoGenerationConfig ReadFile()
        {
            return ReadFrom(DefaultFilePath);
        }

        /// <summary>
        /// Load the configuration from the specified file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
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
