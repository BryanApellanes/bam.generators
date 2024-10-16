using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Bam;
using Bam.Logging;
using Bam.ServiceProxy;

namespace Bam.Generators
{
    public abstract class AssemblyGenerator : Loggable, IAssemblyGenerator
    {
        public AssemblyGenerator()
        {
            _generatedAssemblies = new Dictionary<string, GeneratedAssemblyInfo>();
            _sourceHashes = new Dictionary<string, string>();
            HashAlgorithm = HashAlgorithms.SHA1;
        }
        /// <summary>
        /// The name of the assembly to generate, if this values is null a random name is generated.
        /// </summary>
        public string AssemblyName { get; set; }
        public string SourceDirectoryPath { get; set; }

        public string GeneratorType
        {
            get { return GetType().Name; }
        }

        /// <summary>
        /// Metadata file holding GeneratedAssemblyInfo
        /// </summary>
        public string InfoFileName { get; set; }
        public HashAlgorithms HashAlgorithm { get; set; }
        public string Seed { get; set; }

        /// <summary>
        /// The path to the geninfo file which holds meta data about
        /// the Assembly generated by this generator.
        /// </summary>
        public string GeneratedAssemblyInfoPath { get; set; }

        public GeneratedAssemblyInfo GenerateAssembly()
        {
            return GenerateAssembly(out byte[] ignore);
        }

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
        public void WriteSource()
        {
            WriteSource(SourceDirectoryPath);
            _sourceWritten = true;
            FireEvent(SourceWritten);
        }

        [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "SourceWritten({GeneratorType}):AssemblyName:{AssemblyName}\r\nSourceDirectoryPath:{SourceDirectoryPath}")]
        public event EventHandler SourceWritten;

        [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "AssemblyCompiled({GeneratorType}):AssemblyName:{AssemblyName}\r\nSourceDirectoryPath:{SourceDirectoryPath}")]
        public event EventHandler AssemblyCompiled;

        [Verbosity(VerbosityLevel.Information, SenderMessageFormat = "AssemblySaved({GeneratorType}):AssemblyName:{AssemblyName}\r\nSourceDirectoryPath:{SourceDirectoryPath}")]
        public event EventHandler AssemblySaved;

        public abstract void WriteSource(string writeSourceDir);

        public abstract Assembly CompileAssembly(out byte[] bytes);

        protected bool FilesHashed { get; set; }

        static Dictionary<string, GeneratedAssemblyInfo> _generatedAssemblies;
        protected virtual Assembly GetAssembly(out byte[] bytes)
        {
            string sourceHash = HashSource(!FilesHashed);
            InfoFileName = sourceHash;
            if (_generatedAssemblies.ContainsKey(sourceHash))
            {
                GeneratedAssemblyInfo result = _generatedAssemblies[sourceHash];
                bytes = result.AssemblyBytes;
                return result;
            }

            Assembly compiled = CompileAssembly(out bytes);
            FireEvent(AssemblyCompiled);
            SaveAssemblyFile(sourceHash, bytes);
            FireEvent(AssemblySaved);

            _generatedAssemblies.AddMissing(sourceHash, new GeneratedAssemblyInfo(InfoFileName, compiled, bytes));
            return compiled;
        }

        Dictionary<string, string> _sourceHashes;
        /// <summary>
        /// Calculates the SHA1 hash for all source files found, one at a time, concatenating each to the result
        /// of the previous operation. 
        /// </summary>
        /// <returns></returns>
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
        object _hashFileLock = new object();
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
            RuntimeConfig config = RuntimeSettings.GetRuntimeConfig();
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