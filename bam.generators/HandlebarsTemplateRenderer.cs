﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Bam.Logging;
using Bam;

namespace Bam.Generators
{
    public class HandlebarsTemplateRenderer : ITemplateRenderer
    {
        public HandlebarsTemplateRenderer(string directoryPath = ".") : this(
            new HandlebarsEmbeddedResources(typeof(HandlebarsTemplateRenderer).Assembly),
            new HandlebarsDirectory(directoryPath))
        {
        }
        
        public HandlebarsTemplateRenderer(HandlebarsEmbeddedResources handlebarsEmbeddedResources, HandlebarsDirectory handlebarsDirectory)
            : this(handlebarsEmbeddedResources, new HandlebarsDirectory[] {handlebarsDirectory})
        {
        }
        
        public HandlebarsTemplateRenderer(HandlebarsEmbeddedResources handlebarsEmbeddedResources, params HandlebarsDirectory[] handlebarsDirectories)
        {
            HandlebarsDirectories = new HashSet<HandlebarsDirectory>();
            HandlebarsEmbeddedResources = handlebarsEmbeddedResources;
            
            HandlebarsEmbeddedResources.Reload();
            
            foreach (HandlebarsDirectory handlebarsDirectory in handlebarsDirectories)
            {
                handlebarsDirectory.Reload();
                HandlebarsDirectories.Add(handlebarsDirectory);
            }
        }

        public ILogger Logger { get; set; }
        
        public HashSet<HandlebarsDirectory> HandlebarsDirectories { get; set; }
        public HandlebarsEmbeddedResources HandlebarsEmbeddedResources { get; set; }
        
        public void AddDirectory(DirectoryInfo directoryInfo)
        {
            HandlebarsDirectories.Add(new HandlebarsDirectory(directoryInfo, Logger));
        }

        public string Render(object? toRender)
        {
            if(toRender == null)
            {
                throw new ArgumentNullException(nameof(toRender));
            }

            return Render(toRender.GetType().Name, toRender);
        }

        public string Render(string templateName, object? data)
        {            
            MemoryStream ms = new MemoryStream();
            try
            {
                Render(templateName, data, ms, false);
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ReadToEnd();
            }
            finally
            {
                ms.Dispose();
            }
        }
        
        public void Render(object? toRender, Stream output)
        {
            Args.ThrowIfNull(toRender, "toRender");

#pragma warning disable CS8602 // Dereference of a possibly null reference.  Previous line will throw if toRender is null
            Render(toRender.GetType().Name, toRender, output);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        public void Render(string templateName, object? renderModel, Stream output)
        {
            Render(templateName, renderModel, output, false);
        }
        
        public void Render(string templateName, object? renderModel, Stream output, bool dispose = false)
        {
            if(renderModel == null)
            {
                return;
            }

            if(HandlebarsEmbeddedResources != null && HandlebarsEmbeddedResources.Templates.Count == 0)
            {
                HandlebarsEmbeddedResources.Reload();
            }

            HandlebarsDirectory handlebarsDirectory = GetHandlebarsDirectory(templateName);
            if (handlebarsDirectory != null)
            { 
                string code = handlebarsDirectory.Render(templateName, renderModel);
                code.WriteToStream(output, dispose);
            }
            else if ((HandlebarsEmbeddedResources?.Templates?.ContainsKey(templateName)) == true)
            {
                string code = HandlebarsEmbeddedResources.Render(templateName, renderModel);
                code.WriteToStream(output, dispose);
            }
            else
            {
                Args.Throw<InvalidOperationException>("Specified template '{0}' not found", templateName);
            }
        }

        private HandlebarsDirectory GetHandlebarsDirectory(string templateName)
        {
            HandlebarsDirectory toUse = HandlebarsDirectories.FirstOrDefault(h => h.HasTemplate(templateName));
            if (HandlebarsDirectories.Count(h => h.HasTemplate(templateName)) > 1)
            {
                (Logger ?? Log.Default).Info("Multiple templates named {0} were found, using {1}", templateName, Path.Combine(toUse.Directory.FullName, templateName));
            }

            return toUse;
        }
    }
}
