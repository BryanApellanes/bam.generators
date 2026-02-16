using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bam.CommandLine;
using Bam.Data.Repositories;
using System.CodeDom.Compiler;
using Google.Protobuf;
using Bam.CommandLine;
using Bam;
using Bam.CoreServices.ProtoBuf;

namespace Bam.Generators
{
    /// <summary>
    /// A class used to generate source and/or an assembly
    /// containing CSharp protocol buffer classes
    /// </summary>
    public class ProtocolBuffersAssemblyGenerator: IAssemblyGenerator
    {
        /// <summary>
        /// Initializes a new instance with the default assembly name "Bam.Generated.ProtocolBuffers".
        /// </summary>
        public ProtocolBuffersAssemblyGenerator()
            : this("Bam.Generated.ProtocolBuffers")
        { }

        /// <summary>
        /// Initializes a new instance with the specified assembly name and optional types to include.
        /// </summary>
        /// <param name="assemblyName">The name for the generated assembly.</param>
        /// <param name="types">The CLR types to generate protocol buffer classes for.</param>
        public ProtocolBuffersAssemblyGenerator(string assemblyName, params Type[] types)
            :this(new ProtoFileGenerator())
        {
            AssemblyName = assemblyName;
            if(types.Length > 0)
            {
                AddTypes(types);
            }
        }

        /// <summary>
        /// Initializes a new instance with the specified proto file generator and optional property filter.
        /// </summary>
        /// <param name="protoFileGenerator">The generator that produces .proto files from CLR types.</param>
        /// <param name="propertyFilter">An optional predicate to filter which properties are included in the proto definitions.</param>
        public ProtocolBuffersAssemblyGenerator(ProtoFileGenerator protoFileGenerator, Func<PropertyInfo, bool> propertyFilter = null)
        {
            CompilerPath = ".\\protoc.exe";
            ProtoFileGenerator = protoFileGenerator;
            CsFileDirectory = ".\\Generated_Protobuf_Cs";
            if(propertyFilter != null)
            {
                PropertyFilter = propertyFilter;
            }
            Types = new HashSet<Type>();
        }

        /// <summary>
        /// Gets or sets the proto file generator used to produce .proto files from CLR types.
        /// </summary>
        protected ProtoFileGenerator ProtoFileGenerator { get; set; }

        /// <summary>
        /// Gets or sets the file path to the protoc compiler executable.
        /// </summary>
        public string CompilerPath { get; set; }

        /// <summary>
        /// Gets or sets the directory where generated C# files are written by the protoc compiler.
        /// </summary>
        public string CsFileDirectory { get; set; }

        /// <summary>
        /// Gets or sets the name for the generated assembly.
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// Gets or sets the CLR types to generate protocol buffer classes for.
        /// </summary>
        public HashSet<Type> Types { get; set; }

        /// <summary>
        /// Adds the specified types to the set of types to generate protocol buffer classes for.
        /// </summary>
        /// <param name="types">The types to add.</param>
        public void AddTypes(IEnumerable<Type> types)
        {
            types.Each(t => Types.Add(t));
        }
        /// <summary>
        /// Gets or sets the output directory for generated .proto files.
        /// </summary>
        public string ProtoFileDirectory
        {
            get
            {
                return ProtoFileGenerator.OutputDirectory;
            }
            set
            {
                ProtoFileGenerator.OutputDirectory = value;
            }
        }

        /// <summary>
        /// Gets or sets the filter predicate that determines which properties are included in proto definitions.
        /// </summary>
        public Func<PropertyInfo, bool> PropertyFilter
        {
            get
            {
                return ProtoFileGenerator.PropertyFilter;
            }
            set
            {
                ProtoFileGenerator.PropertyFilter = value;
            }
        }

        /// <summary>
        /// Gets or loads the generated assembly from cache based on the assembly name.
        /// </summary>
        /// <returns>The generated or cached <see cref="Assembly"/>.</returns>
        public Assembly GetAssembly()
        {
            return GeneratedAssemblyInfo.GetGeneratedAssembly(AssemblyName, this);
        }
        /// <summary>
        /// Generates C# files from the currently registered types using the protoc compiler.
        /// </summary>
        /// <returns>The <see cref="DirectoryInfo"/> for the directory containing the generated C# files.</returns>
        public DirectoryInfo GenerateCsFiles()
        {
            return GenerateCsFiles(Types.ToArray());
        }

        /// <summary>
        /// Generates C# files from the specified types using the protoc compiler.
        /// </summary>
        /// <param name="types">The CLR types to generate protocol buffer C# files for.</param>
        /// <returns>The <see cref="DirectoryInfo"/> for the directory containing the generated C# files.</returns>
        public DirectoryInfo GenerateCsFiles(params Type[] types)
        {
            return GenerateCsFiles(types, (o, a) => { });
        }

        /// <summary>
        /// Generates C# files from a repository's storable types.
        /// </summary>
        /// <param name="repo">The repository whose storable types to generate protocol buffer C# files for.</param>
        /// <returns>The <see cref="DirectoryInfo"/> for the directory containing the generated C# files.</returns>
        public DirectoryInfo GenerateCsFiles(IRepository repo)
        {
            return GenerateCsFiles(repo.StorableTypes);
        }

        /// <summary>
        /// Generates C# files from the specified types and invokes a callback upon completion.
        /// </summary>
        /// <param name="clrTypes">The CLR types to generate protocol buffer C# files for.</param>
        /// <param name="onComplete">The callback to invoke when generation completes.</param>
        /// <returns>The <see cref="DirectoryInfo"/> for the directory containing the generated C# files.</returns>
        public DirectoryInfo GenerateCsFiles(IEnumerable<Type> clrTypes, EventHandler onComplete)
        {
            return GenerateCsFiles(clrTypes, CsFileDirectory, onComplete);
        }

        /// <summary>
        /// Generates C# files from the specified types, writing to the specified directory.
        /// </summary>
        /// <param name="clrTypes">The CLR types to generate protocol buffer C# files for.</param>
        /// <param name="csFileDirectory">The output directory for generated C# files. If null, uses the current <see cref="CsFileDirectory"/>.</param>
        /// <param name="onComplete">An optional callback to invoke when generation completes.</param>
        /// <returns>The <see cref="DirectoryInfo"/> for the directory containing the generated C# files.</returns>
        public DirectoryInfo GenerateCsFiles(IEnumerable<Type> clrTypes, string csFileDirectory = null, EventHandler onComplete = null)
        {
            CsFileDirectory = csFileDirectory ?? CsFileDirectory;
            HashSet<Type> types = new HashSet<Type>();
            if (clrTypes != null)
            {
                clrTypes.Each(t => types.Add(t));
            }
            Types = types;
            DirectoryInfo dir = GenerateCsFiles(onComplete);
            return dir;
        }

        /// <summary>
        /// Generates .proto files from the current types, invokes the protoc compiler to produce C# files,
        /// and optionally calls the completion handler.
        /// </summary>
        /// <param name="onComplete">An optional callback to invoke when the protoc compiler finishes.</param>
        /// <returns>The <see cref="DirectoryInfo"/> for the directory containing the generated C# files.</returns>
        public DirectoryInfo GenerateCsFiles(EventHandler onComplete = null)
        {
            string protoFilePath = ProtoFileGenerator.GenerateProtoFile(Types);
            DirectoryInfo dir = new DirectoryInfo(CsFileDirectory);
            if (Directory.Exists(dir.FullName))
            {
                Directory.Move(dir.FullName, dir.FullName.GetNextDirectoryName());
            }
            dir.Create();
            FileInfo compiler = new FileInfo(CompilerPath);
            string command = $"{compiler.FullName} -I={compiler.Directory.FullName} --csharp_out={dir.FullName} {protoFilePath}";
            ProcessOutput output = null;
            AutoResetEvent wait = new AutoResetEvent(false);
            output = command.Run((o, a) =>
            {
                onComplete?.Invoke(o, a);
                wait.Set();
            });
            wait.WaitOne();
            return dir;
        }

        /// <summary>
        /// Generates C# protocol buffer files and compiles them into an assembly using Roslyn.
        /// Blocks until compilation completes.
        /// </summary>
        /// <returns>A <see cref="GeneratedAssemblyInfo"/> containing the compiled assembly, or null if compilation fails.</returns>
        public GeneratedAssemblyInfo? GenerateAssembly()
        {
            Args.ThrowIfNullOrEmpty(AssemblyName, nameof(AssemblyName));

            GeneratedAssemblyInfo? result = null;
            AutoResetEvent wait = new AutoResetEvent(false);
            GenerateCsFiles(Types, (o, a) =>
            {
                RoslynCompiler compiler = new RoslynCompiler();
                compiler.AddMetadataReferenceResolver(new StaticAssemblyListReferencePackMetadataReferenceResolver(typeof(IMessage).Assembly.GetFileInfo().FullName));
                byte[] assemblyBytes = compiler.CompileDirectories(AssemblyName, new DirectoryInfo(CsFileDirectory));
                result = new GeneratedAssemblyInfo(AssemblyName, Assembly.Load(assemblyBytes), assemblyBytes);
                wait.Set();
            });
            wait.WaitOne();
            return result;
        }

        /// <summary>
        /// Generates C# protocol buffer source files into the specified directory.
        /// </summary>
        /// <param name="writeSourceDir">The directory to write the generated source files to.</param>
        public void WriteSource(string writeSourceDir)
        {
            CsFileDirectory = writeSourceDir;
            GenerateCsFiles();
        }
    }
}
