﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Bam.Net.CoreServices.AssemblyManagement;
using Bam.Net.Data.Schema;
using Bam.Net.Data.Schema.Handlebars;
using Bam.Net.Logging;
using Bam.Net.Services;
using Newtonsoft.Json;

namespace Bam.Net.Data.Repositories.Handlebars
{
    // TODO: review if this can be deleted as redundant
    public class HandlebarsTypeToDaoGenerator: TypeToDaoGenerator
    {
        public HandlebarsTypeToDaoGenerator(ISchemaProvider schemaProvider, IDaoGenerator daoGenerator, ILogger? logger = null) : base(schemaProvider, daoGenerator, null, logger)
        {
            SchemaProvider = schemaProvider;
            SetWrapperGenerator(new HandlebarsWrapperGenerator(schemaProvider));
        }

        protected override bool GenerateDaoAssembly(ITypeSchema typeSchema, out Exception compilationEx)
        {
            compilationEx = null;
            try
            {
                IDaoSchemaDefinition schema = SchemaDefinitionCreateResult.SchemaDefinition;
                string assemblyName = $"{schema.Name}.dll";

                string writeSourceTo = TypeSchemaTempPathProvider(schema, typeSchema);
                TryDeleteDaoTemp(writeSourceTo);
                GenerateSource(writeSourceTo);
                byte[] assemblyBytes = Compile(assemblyName, writeSourceTo);
                Assembly assembly = Assembly.Load(assemblyBytes);
                GeneratedDaoAssemblyInfo info =
                    new GeneratedDaoAssemblyInfo(schema.Name, assembly, assemblyBytes)
                    {
                        TypeSchema = typeSchema,
                        SchemaDefinition = schema
                    };

                info.Save();

                GeneratedAssemblies.SetAssemblyInfo(schema.Name, info);

                Message = "Type Dao Generation completed successfully";
                FireGenerateDaoAssemblySucceeded(new GenerateDaoAssemblyEventArgs(info));

                TryDeleteDaoTemp(writeSourceTo);

                return true;
            }
            catch (Exception ex)
            {
                Message = ex.Message;
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    Message = ex.GetMessageAndStackTrace();
                }

                FireGenerateDaoAssemblyFailed(ex);
                return false;
            }
        }

        protected override HashSet<string> GetDefaultReferenceAssemblies()
        {
            /*IReferenceAssemblyResolver referenceAssemblyResolver = ReferenceAssemblyResolver ?? CoreServices.AssemblyManagement.ReferenceAssemblyResolver.Current;*/
            
            HashSet<string> result = new HashSet<string>()
            {
                typeof(JsonConvert).Assembly.GetFilePath(),
                typeof(MarshalByValueComponent).Assembly.GetFilePath(),
                typeof(Enumerable).Assembly.GetFilePath(),
                typeof(Object).Assembly.GetFilePath(),
                //referenceAssemblyResolver.ResolveReferenceAssemblyPath("System.Collections.dll"),
                //referenceAssemblyResolver.ResolveReferenceAssemblyPath("netstandard.dll"),
                typeof(Attribute).Assembly.GetFilePath(),
                //referenceAssemblyResolver.ResolveSystemRuntimePath()
            };
            return result;
        }
/*
        private byte[] Compile(string assemblyNameToCreate, string sourcePath)
        {
            HashSet<string> references = GetReferenceAssemblies();
            RoslynCompiler compiler = new RoslynCompiler();
            references.Each(path => compiler.AddAssemblyReference(path));
            return compiler.Compile(assemblyNameToCreate, new DirectoryInfo(sourcePath));
        }*/
    }
}
