using System;
using System.Collections.Generic;
using System.Text;
using Bam.Net;
using Bam.Net.Data.Repositories;

namespace Bam.Generators
{
    public class HandlebarsSchemaTypeModel: SchemaTypeModel
    {
        public string TypeNamePluralized
        {
            get
            {
                return Type.Name.Pluralize();
            }
        }

        public new static HandlebarsSchemaTypeModel FromType(Type type, string daoNamespace)
        {
            return new HandlebarsSchemaTypeModel { Type = type, DaoNamespace = daoNamespace };
        }
    }
}
