using System;
using System.Linq;
using Bam.Data.Repositories;
using Bam;
using Bam.Data.Repositories;

namespace Bam.Generators
{
    public class HandlebarsWrapperModel : WrapperModel
    {
        public HandlebarsWrapperModel(Type pocoType, ITypeSchema schema, string wrapperNamespace = "TypeWrappers", string daoNameSpace = "Daos") : base(pocoType, schema, wrapperNamespace, daoNameSpace)
        {
            ForeignKeys = ForeignKeys.Select(fk => fk.CopyAs<HandlebarsTypeFkModel>()).ToArray();
            ChildPrimaryKeys = ChildPrimaryKeys.Select(fk => fk.CopyAs<HandlebarsTypeFkModel>()).ToArray();
            LeftXrefs = LeftXrefs.Select(lxref => lxref.CopyAs<HandlebarsTypeXrefModel>()).ToArray();
            RightXrefs = RightXrefs.Select(rx => rx.CopyAs<HandlebarsTypeXrefModel>()).ToArray();

            TemplateRenderer = new HandlebarsTemplateRenderer();
        }

        public override string Render()
        {
            return TemplateRenderer.Render("Wrapper", this);
        }
    }
}