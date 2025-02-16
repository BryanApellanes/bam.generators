using Bam.Data.Repositories;

namespace Bam.Generators
{
    public class ListFileReferencePackMetaDataReferenceResolver : AssemblyListReferencePackMetadataReferenceResolver
    {
        public ListFileReferencePackMetaDataReferenceResolver() 
        {
            this.FilePath = "./.bam-assembly-ref";
        }

        /// <summary>
        /// Gets or sets the file path to read.
        /// </summary>
        public string FilePath { get; set; }

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
