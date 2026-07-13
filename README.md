# bam.generators

Handlebars-based code generation for the Bam Framework — DAOs, service clients, and UX sites.

## Overview

`bam.generators` is the templating engine behind the framework's code-generation tools. Its core project wraps [Handlebars.Net](https://github.com/Handlebars-Net/Handlebars.Net) with a set of embedded `.hbs` templates (`Class`, `Collection`, `Dto`, `SchemaRepository`, `Wrapper`, `QueryClass`, foreign-key/xref property partials, and more) and the machinery to render them against a schema: `HandlebarsSchemaRepositoryGenerator`, `HandlebarsWrapperGenerator`, `HandlebarsCSharpDaoCodeWriter`, and the `DaoRepoGenerationConfig` type that drives generation from a YAML config. This is the engine `bamdb` invokes to turn POCO types into DAO source.

Two sibling projects extend the same template-driven approach to other generation targets:
- **`bam.generators.client`** generates strongly-typed service client code from a `bam.protocol`/`bam.server` service definition (`BamServiceClientGenerator`, `BamServiceClientModel`, `ServiceClientCodeWriter`).
- **`bam.generators.ux`** generates UX site scaffolding — pages, navigation, themes, and color palettes — from a `UxGenerationConfig` (`UxSiteGenerator`, `HandlebarsUxSiteWriter`, `ColorPaletteGenerator`).

## Key Classes

| Class | Description |
|---|---|
| `HandlebarsSchemaRepositoryGenerator` | Renders a complete schema repository (DAO classes, collections, query classes) from an in-memory schema model. |
| `HandlebarsWrapperGenerator` | Generates wrapper classes over generated DAOs. |
| `DaoRepoGenerationConfig` | YAML-driven configuration for a DAO generation run (source assembly/namespace, target namespace, output directory). |
| `HandlebarsTemplateSet` / `HandlebarsEmbeddedResources` | Loads the embedded `.hbs` templates by name. |
| `BamServiceClientGenerator` (client) | Generates a strongly-typed client for a Bam protocol service. |
| `UxSiteGenerator` (ux) | Generates a UX site's pages/navigation/theme from a `UxGenerationConfig`. |

## Dependencies

**Package References:** `Handlebars.Net`.

**Core project references:** `bam.application`, `bam.base`, `bam.configuration`, `bam.data.repositories`, `bam.data.schema`, `bam.data` (and its provider projects: config, firebird, mssql, mysql, oracle, postgres), `bam.logging`.

**Target Framework:** net10.0.

## Running Tests

```bash
dotnet run --project bam.generators.tests/bam.generators.tests.csproj -- --ut
dotnet run --project bam.generators.client.tests/bam.generators.client.tests.csproj -- --ut
dotnet run --project bam.generators.ux.tests/bam.generators.ux.tests.csproj -- --ut
```
