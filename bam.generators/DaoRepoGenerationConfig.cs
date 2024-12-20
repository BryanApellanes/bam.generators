using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Bam.Data;
using Bam;
using Bam.Configuration;
using Bam.Data.Repositories;

namespace Bam.Application
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
        /// Gets or sets the namespace to generate dao types to.
        /// </summary>
        [CompositeKey]
        public string ToNamespace { get; set; }

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
        /// Load the config from the file ./dao-repo-gen.yaml.
        /// </summary>
        /// <returns></returns>
        public static DaoRepoGenerationConfig LoadDefault()
        {
            return LoadFrom("./dao-repo-gen.yaml");
        }

        /// <summary>
        /// Load the configuration from the specified file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static DaoRepoGenerationConfig LoadFrom(string path)
        {
            return LoadFrom(new FileInfo(path));
        }

        /// <summary>
        /// Load the configuration from the specified file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns>DaoRepoGenerationConfig</returns>
        public static DaoRepoGenerationConfig LoadFrom(FileInfo file)
        {
            if(!file.Exists)
            {
                throw new ArgumentException($"The specified dao repo generation config file was not found: {file.FullName}");
            }

            string ext = file.Extension;
            if (ext.ToLowerInvariant().Equals(".json"))
            {
                return file.FullName.SafeReadFile().FromJson<DaoRepoGenerationConfig>();
            }
            if(ext.ToLowerInvariant().Equals(".yml") || ext.ToLowerInvariant().Equals(".yaml"))
            {

                return file.FullName.SafeReadFile().FromYaml<DaoRepoGenerationConfig>();
            }

            throw new ArgumentException($"Unsupported file extension, must be one of (.json, .yml or .yaml) but was: {ext}");
        }

    }
}
