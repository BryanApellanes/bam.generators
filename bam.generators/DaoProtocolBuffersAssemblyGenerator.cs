using Bam.CoreServices.ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators
{
    /// <summary>
    /// A ProtocolBuffersAssemblyGenerator that only includes properties adorned with the ColumnAttribute,
    /// generating protocol buffer types specifically for DAO classes.
    /// </summary>
    public class DaoProtocolBuffersAssemblyGenerator : ProtocolBuffersAssemblyGenerator
    {
        /// <summary>
        /// Initializes a new instance using an <see cref="InMemoryPropertyNumberer"/> for proto field numbering.
        /// </summary>
        public DaoProtocolBuffersAssemblyGenerator()
            : base(new DaoProtoFileGenerator(new InMemoryPropertyNumberer()))
        { }

        /// <summary>
        /// Initializes a new instance using the specified property numberer for proto field numbering.
        /// </summary>
        /// <param name="propertyNumberer">The property numberer to assign field numbers in the generated proto files.</param>
        public DaoProtocolBuffersAssemblyGenerator(IPropertyNumberer propertyNumberer)
            : base(new DaoProtoFileGenerator(propertyNumberer))
        { }
    }
}
