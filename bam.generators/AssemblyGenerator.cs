using System.Reflection;
using Bam.Logging;

namespace Bam.Generators
{
    /// <summary>
    /// Abstract base class for generating and compiling .NET assemblies from source code.
    /// Provides source writing, content hashing for caching, and assembly compilation infrastructure.
    /// </summary>
    public abstract class AssemblyGenerator : Loggable, IAssemblyGenerator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyGenerator"/> class with default settings,
        /// including SHA1 hashing and a source directory under the BamDir .gen folder.
        /// </summary>
        public AssemblyGenerator()
        {
            _generatedAssemblies = new Dictionary<string, GeneratedAssemblyInfo>();
            _sourceHashes = new Dictionary<string, string>();
            HashAlgorithm = HashAlgorithms.SHA1;
            this.SourceDirectoryPath = Path.Combine(BamDir.Path, ".gen");
        }
        /// <summary>
        /// The name of the assembly to generate, if this values is null a random name is generated.
        /// </summary>
        public string AssemblyName { get; set; }
        /// <summary>
        /// Gets or sets the directory path where generated source files are written.
        /// </summary>
        public string SourceDirectoryPath { get; set; }

        /// <summary>
        /// Gets the name of the concrete generator type.
        /// </summary>
        public string GeneratorType => GetType().Name;

        /// <summary>
        /// Metadata file holding GeneratedAssemblyInfo
        /// </summary>
        public string InfoFileName { get; set; }
        /// <summary>
        /// Gets or sets the hash algorithm used for source file content hashing.
        /// </summary>
        public HashAlgorithms HashAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets an optional seed value used as the initial input when computing the cumulative source hash.
        /// </summary>
        public string Seed { get; set; }

        /// <summary>
        /// Generates an assembly from the source files, writing source first if not already written.
        /// </summary>
        /// <returns>A <see cref="GeneratedAssemblyInfo"/> containing metadata about the generated assembly.</returns>
        public GeneratedAssemblyInfo GenerateAssembly()
        {
            return GenerateAssembly(out byte[] ignore);
        }

        /// <summary>
        /// Generates an assembly from the source files, writing source first if not already written.
        /// </summary>
        /// <param name="bytes">The raw bytes of the compiled assembly.</param>
        /// <returns>A <see cref="GeneratedAssemblyInfo"/> containing metadata about the generated assembly.</returns>
        public GeneratedAssemblyInfo GenerateAssembly(out byte[] bytes)
        {
            if (!_sourceWritten)
            {
                WriteSource();
            }

            Assembly assembly = GetAssembly(out bytes);
            GeneratedAssemblyInfo result = new GeneratedAssemblyInfo(InfoFileName, assembly, bytes);
            result.Save();
            return result;
        }

        bool _sourceWritten;
        /// <summary>
        /// Writes source files to the configured <see cref="SourceDirectoryPath"/> and raises the <see cref="SourceWritten"/> event.
        /// </summary>
        public void WriteSource()
        {
            WriteSource(SourceDirectoryPath);
            _sourceWritten = true;
            FireEvent(SourceWritten);
        }

        /// <summary>
        /// Occurs when source files have been written to the source directory.
        /// </summary>
        [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "SourceWritten({GeneratorType}):AssemblyName:{AssemblyName}\r\nSourceDirectoryPath:{SourceDirectoryPath}")]
        public event EventHandler SourceWritten;

        /// <summary>
        /// Occurs when the assembly has been compiled from source.
        /// </summary>
        [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "AssemblyCompiled({GeneratorType}):AssemblyName:{AssemblyName}\r\nSourceDirectoryPath:{SourceDirectoryPath}")]
        public event EventHandler AssemblyCompiled;

        /// <summary>
        /// Occurs when the compiled assembly has been saved to disk.
        /// </summary>
        [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "AssemblySaved({GeneratorType}):AssemblyName:{AssemblyName}\r\nSourceDirectoryPath:{SourceDirectoryPath}")]
        public event EventHandler AssemblySaved;

        /// <summary>
        /// When overridden in a derived class, writes the generated source files to the specified directory.
        /// </summary>
        /// <param name="writeSourceDir">The directory path to write source files to.</param>
        public abstract void WriteSource(string writeSourceDir);

        /// <summary>
        /// When overridden in a derived class, compiles the generated source files into an assembly.
        /// </summary>
        /// <param name="bytes">The raw bytes of the compiled assembly.</param>
        /// <returns>The compiled <see cref="Assembly"/>.</returns>
        public abstract Assembly CompileAssembly(out byte[] bytes);

        /// <summary>
        /// Gets or sets a value indicating whether source files have already been hashed.
        /// </summary>
        protected bool FilesHashed { get; set; }

        private static Dictionary<string, GeneratedAssemblyInfo>? _generatedAssemblies;
        protected virtual Assembly GetAssembly(out byte[] bytes)
        {
            string sourceHash = HashSource(!FilesHashed);
            InfoFileName = sourceHash;
            if (_generatedAssemblies != null && _generatedAssemblies.TryGetValue(sourceHash, out GeneratedAssemblyInfo? result))
            {
                bytes = result.AssemblyBytes;
                return result;
            }

            Assembly compiled = CompileAssembly(out bytes);
            FireEvent(AssemblyCompiled);
            SaveAssemblyFile(sourceHash, bytes);
            FireEvent(AssemblySaved);

            if (_generatedAssemblies != null)
            {
                _generatedAssemblies.TryAdd(sourceHash, new GeneratedAssemblyInfo(InfoFileName, compiled, bytes));   
            }
            return compiled;
        }

        Dictionary<string, string> _sourceHashes;
        /// <summary>
        /// Calculates the SHA1 hash for all source files found, one at a time, concatenating each to the result
        /// of the previous operation. 
        /// </summary>
        /// <param name="rehashFiles">If true, re-reads and hashes all source files before computing the cumulative hash.</param>
        /// <returns>A hexadecimal hash string representing the combined content of all source files.</returns>
        protected string HashSource(bool rehashFiles = true)
        {
            if (rehashFiles)
            {
                HashFiles();
            }

            DirectoryInfo sourceDirectory = new DirectoryInfo(SourceDirectoryPath);
            SortedSet<string> sortedFilePaths = new SortedSet<string>();
            sourceDirectory.GetFiles("*.cs").Each(fi => sortedFilePaths.Add(fi.FullName));
            string currentHash = (Seed ?? "").HashHexString(HashAlgorithm);
            foreach (string filePath in sortedFilePaths)
            {
                FileInfo file = new FileInfo(filePath);
                currentHash = $"{currentHash}{HashFile(filePath)}".HashHexString(HashAlgorithm);
            }

            return currentHash;
        }

        Dictionary<string, string> _fileHashes;
        readonly object _hashFileLock = new object();
        /// <summary>
        /// Hashes all .cs files in the source directory and caches the results.
        /// </summary>
        protected void HashFiles()
        {
            lock (_hashFileLock)
            {
                _fileHashes = new Dictionary<string, string>();
                DirectoryInfo sourceDirectory = new DirectoryInfo(SourceDirectoryPath);
                if (sourceDirectory.Exists)
                {
                    foreach (FileInfo file in sourceDirectory.GetFiles("*.cs"))
                    {
                        HashFile(file.FullName);
                    }

                    FilesHashed = true;
                }
            }
        }

        private string HashFile(string filePath)
        {
            if (_fileHashes.ContainsKey(filePath))
            {
                return _fileHashes[filePath];
            }

            string contentHash = new FileInfo(filePath).ContentHash(HashAlgorithm);
            _fileHashes.AddMissing(filePath, contentHash);
            return contentHash;
        }

        private void SaveAssemblyFile(string sourceHash, byte[] bytes)
        {
            RuntimeConfig config = RuntimeConfig.Current;
            if (!Directory.Exists(config.GenDir))
            {
                Directory.CreateDirectory(config.GenDir);
            }

            if (string.IsNullOrEmpty(AssemblyName))
            {
                AssemblyName = sourceHash;
            }

            string assemblyFile = Path.Combine(config.GenDir, AssemblyName);
            File.WriteAllBytes(assemblyFile, bytes);
        }
    }
}