using Bam.Data.Repositories;

namespace Bam.Generators
{
    /// <summary>
    /// Resolves assembly metadata references by reading assembly file names from a newline-delimited text file.
    /// Defaults to reading from "./.bam-assembly-ref".
    /// </summary>
    public class ListFileReferencePackMetaDataReferenceResolver : AssemblyListReferencePackMetadataReferenceResolver
    {
        /// <summary>
        /// Initializes a new instance with the default file path "./.bam-assembly-ref".
        /// </summary>
        public ListFileReferencePackMetaDataReferenceResolver()
        {
            this.FilePath = "./.bam-assembly-ref";
        }

        /// <summary>
        /// Gets or sets the file path to read.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Reads the assembly reference file and returns its contents as an array of assembly file names,
        /// split by newline characters. Returns an empty array if the file does not exist.
        /// </summary>
        /// <returns>An array of assembly file name strings.</returns>
        public override string[] GetAssemblyFileNames()
        {
            if (!File.Exists(this.FilePath))
            {
                return new string[0];
            }

            string fileContent = File.ReadAllText(this.FilePath);
            return fileContent.Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
