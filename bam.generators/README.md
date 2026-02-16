# bam.generators

Handlebars-based code generation library for producing DAO (Data Access Object) classes, wrapper classes, schema repositories, and related data access artifacts from POCO types.

## Overview

bam.generators is the code generation engine of the Bam Framework. It uses [Handlebars.Net](https://github.com/Handlebars-Net/Handlebars.Net) templates to transform plain C# POCO (Plain Old CLR Object) types into a full data access layer, including DAO classes, collection classes, query classes, column metadata classes, context classes, wrapper classes, and schema repository classes. The generated code provides strongly-typed database interaction without requiring developers to write boilerplate data access code by hand.

The library supports loading templates from two sources: the file system (via `HandlebarsDirectory`) and embedded assembly resources (via `HandlebarsEmbeddedResources`). Over 20 `.hbs` template files are shipped as embedded resources covering every artifact type -- from primary DAO classes to foreign key properties and cross-reference (xref) join table models. Templates can be overridden by placing `.hbs` files in a directory on disk, giving consumers full control over the shape of generated code.

A typical workflow involves creating a `DaoRepoGenerationConfig` (usually from a YAML file), passing it to `HandlebarsSchemaRepositoryGenerator`, and calling `GenerateSource()`. This inspects the POCO types in a specified assembly namespace, builds an in-memory schema, and renders all necessary C# source files into a target directory. The `DefaultDaoRepository` class wires up all the required services via a built-in `ServiceRegistry`, making it easy to get started with a single constructor call.

## Key Classes

| Class | Description |
|---|---|
| `HandlebarsCSharpDaoCodeWriter` | Implements `IDaoCodeWriter`; renders DAO class, collection, columns, context, query, paged query, QI, and partial files from Handlebars templates. |
| `HandlebarsSchemaRepositoryGenerator` | Orchestrates end-to-end source generation: reads config, builds schema from POCO types, and writes a full schema repository with typed query methods. |
| `HandlebarsSchemaRepositoryGeneratorSettings` | Configures the generator with the appropriate code writer, stream resolver, and wrapper generator via a `ServiceRegistry`. |
| `HandlebarsWrapperGenerator` | Generates wrapper classes that bridge POCO types to their DAO counterparts, compiling them via Roslyn. |
| `HandlebarsWrapperModel` | View-model for the `Wrapper.hbs` template; includes foreign key and xref relationship metadata. |
| `DefaultDaoRepository` | Pre-configured `DaoRepository` that resolves `IDaoCodeWriter`, `ISchemaProvider`, `IDaoGenerator`, and `IWrapperGenerator` from a built-in service registry. |
| `DaoRepoGenerationConfig` | YAML-serializable configuration specifying source assembly, namespaces, output paths, and schema options. |
| `HandlebarsDirectory` | Loads, compiles, and caches `.hbs` template files from a file system directory, with support for partials subdirectories. |
| `HandlebarsEmbeddedResources` | Loads and compiles `.hbs` templates from assembly embedded resources. |
| `HandlebarsTemplateRenderer` | General-purpose renderer that resolves templates from directories and/or embedded resources and renders them to strings or streams. |
| `HandlebarsTemplateRenderer<T>` | Strongly-typed generic variant of `HandlebarsTemplateRenderer`. |
| `HandlebarsTemplateSet` | Convenience class combining multiple `HandlebarsDirectory` instances and embedded resources into a single renderable set. |
| `Handlebars` (static) | Static facade for rendering templates; requires `HandlebarsDirectory` and/or `HandlebarsEmbeddedResources` to be set before use. |
| `HandlebarsSchemaTypeModel` | Extends `SchemaTypeModel` with a `TypeNamePluralized` property for template use. |
| `HandlebarsTypeFkModel` | Extends `TypeFk` with camel-cased and type-string properties for foreign key template rendering. |
| `HandlebarsTypeXrefModel` | Extends `TypeXrefModel` with pluralized and camel-cased name properties for xref template rendering. |
| `AssemblyGenerator` | Abstract base class for source generation and Roslyn compilation with SHA1-based caching of generated assemblies. |
| `ListFileReferencePackMetaDataReferenceResolver` | Reads assembly reference paths from a `.bam-assembly-ref` file for Roslyn compilation. |

## Embedded Templates

The following Handlebars templates are shipped as embedded resources:

| Template | Purpose |
|---|---|
| `Class.hbs` | Main DAO class |
| `Collection.hbs` | DAO collection class |
| `ColumnsClass.hbs` | Columns metadata class |
| `ColumnsProperty.hbs` | Individual column property partial |
| `Context.hbs` | Database context class |
| `ContextMethods.hbs` | Context method partials |
| `Dto.hbs` | Data transfer object |
| `ForeignKeyColumnsProperty.hbs` | FK column property partial |
| `ForeignKeyProperty.hbs` | FK navigation property partial |
| `KeyProperty.hbs` | Primary key property partial |
| `PagedQueryClass.hbs` | Paged query class |
| `Partial.hbs` | User-extensible partial class |
| `Property.hbs` | Standard property partial |
| `QiClass.hbs` | Query item class |
| `QueryClass.hbs` | Query class |
| `SchemaRepository.hbs` | Schema repository class |
| `SchemaRepositoryAddType.hbs` | AddType call in repository |
| `SchemaRepositoryMethods.hbs` | Typed OneWhere/GetOneWhere methods |
| `Wrapper.hbs` | POCO-to-DAO wrapper class |
| `WrapperForeignKeyProperty.hbs` | Wrapper FK property partial |
| `XrefProperty.hbs` | Xref property partial |
| `XrefLeftProperty.hbs` | Left-side xref partial |
| `XrefRightProperty.hbs` | Right-side xref partial |
| `ChildPrimaryKeyProperty.hbs` | Child PK property partial |
| `ChildDaoCollectionAdd.hbs` | Child DAO collection add |
| `ChildXrefCollectionAdd.hbs` | Child xref collection add |
| `DaoCollectionProperty.hbs` | DAO collection property |

## Dependencies

### Project References

- `bam.application`
- `bam.base`
- `bam.configuration`
- `bam.data.repositories`
- `bam.data.schema`
- `bam.data.config`
- `bam.data.firebird`
- `bam.data.mssql`
- `bam.data.mysql`
- `bam.data.oracle`
- `bam.data.postgres`
- `bam.data`
- `bam.logging`

### Package References

- `Handlebars.Net` 2.1.6

### Target Framework

- `net10.0`

## Usage Examples

### Generate DAO Source from a YAML Config

```csharp
using Bam.Generators;

// Load configuration from YAML
DaoRepoGenerationConfig config = DaoRepoGenerationConfig.ReadFrom("./DaoRepoGenerationConfig.yaml");

// Create the generator and produce source files
var generator = new HandlebarsSchemaRepositoryGenerator(config);
generator.GenerateSource();
// Source files are written to config.WriteSourceTo (default: "./Generated_Dao")
```

### YAML Configuration File

```yaml
SchemaName: MySchema
FromNamespace: MyApp.Models
ToNamespace: MyApp.Models.Dao
WriteSourceTo: ./Generated_Dao
CheckForIds: true
UseInheritanceSchema: false
```

### Use DefaultDaoRepository for CRUD Operations

```csharp
using Bam.Generators;

var repo = new DefaultDaoRepository();
repo.AddType(typeof(Person));

// Create
Person created = repo.Create(new Person { Name = "Alice" });

// Retrieve
Person retrieved = repo.Retrieve<Person>(created.Id);

// Update
retrieved.Name = "Bob";
Person updated = repo.Update(retrieved);

// Delete
bool deleted = repo.Delete(updated);
```

### Render a Single Handlebars Template

```csharp
using Bam.Generators;

var renderer = new HandlebarsTemplateRenderer();
string output = renderer.Render("Class", myDaoTableSchemaModel);
```

## Excluded / Legacy Files

- `ProtocolBuffersAssemblyGenerator.cs` -- Excluded from compilation (`<Compile Remove>`). Generates Protocol Buffers C# source using `protoc.exe`. Depends on `Google.Protobuf` and `Bam.CoreServices.ProtoBuf`, which are not currently referenced.
- `DaoProtocolBuffersAssemblyGenerator.cs` -- Excluded from compilation. Subclass that filters to `ColumnAttribute`-adorned properties only.

## Known Gaps / Not Yet Implemented

No `NotImplementedException`, `TODO`, or stub methods were found in the active (compiled) source code. The two Protocol Buffers generators are excluded from compilation and would require additional package references (`Google.Protobuf`, `Bam.CoreServices.ProtoBuf`) to be re-enabled.
