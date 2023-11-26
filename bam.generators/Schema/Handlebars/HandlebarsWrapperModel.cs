using System;
using System.Linq;
using Bam.Data.Repositories;
using Bam.Net.Presentation.Handlebars;

namespace Bam.Net.Data.Repositories.Handlebars
{
    public class HandlebarsWrapperModel : WrapperModel
    {
        public HandlebarsWrapperModel(Type pocoType, ITypeSchema schema, string wrapperNamespace = "TypeWrappers", string daoNameSpace = "Daos") : base(pocoType, schema, wrapperNamespace, daoNameSpace)
        {
            ForeignKeys = ForeignKeys.Select(fk => fk.CopyAs<HandlebarsTypeFkModel>()).ToArray();
            ChildPrimaryKeys = ChildPrimaryKeys.Select(fk => fk.CopyAs<HandlebarsTypeFkModel>()).ToArray();
            LeftXrefs = LeftXrefs.Select(lxref => lxref.CopyAs<HandlebarsTypeXrefModel>()).ToArray();
            RightXrefs = RightXrefs.Select(rx => rx.CopyAs<HandlebarsTypeXrefModel>()).ToArray();

            this.TemplateRenderer = new HandlebarsTemplateRenderer();
        }

        public override string Render()
        {
            return TemplateRenderer.Render("Wrapper", this);
        }
    }
}