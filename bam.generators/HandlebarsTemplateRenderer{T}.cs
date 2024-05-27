using Bam;
using Bam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators
{
    public class HandlebarsTemplateRenderer<T> : HandlebarsTemplateRenderer, ITemplateRenderer<T>
    {
        public string Render(T toRender)
        {
            if(toRender == null)
            {
                throw new ArgumentNullException(nameof(toRender));
            }

            return Render(typeof(T).Name, toRender);
        }

        public string Render(string templateName, T? toRender)
        {
            return base.Render(templateName, toRender);
        }

        public void Render(T? toRender, Stream output)
        {
            base.Render(toRender, output);
        }

        public void Render(string templateName, T? toRender, Stream output)
        {
            base.Render(templateName, toRender, output);
        }
    }
}
