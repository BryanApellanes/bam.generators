# bam.generators.tests

Unit and integration tests for the `bam.generators` code generation library, validating DAO generation, schema creation, wrapper generation, and CRUD operations via `DaoRepository`.

## Overview

bam.generators.tests exercises the core code generation pipeline provided by `bam.generators`. It uses the Bam Framework's menu-driven test runner (`BamConsoleContext.StaticMain`) with the `[UnitTestMenu]` and `[UnitTest]` attributes, rather than xUnit or NUnit. Tests are organized into the `Unit` folder and are executed via `dotnet run --project bam.generators.tests.csproj -- --ut`.

The test suite covers five major areas: (1) verifying that `HandlebarsCSharpDaoCodeWriter` correctly delegates to its `HandlebarsDirectory` and `HandlebarsEmbeddedResources` on load, (2) confirming that `SchemaProvider` builds a `TypeSchema` with the expected table count from POCO types, (3) validating end-to-end schema repository source generation from a YAML config, (4) testing full CRUD lifecycle (create, retrieve, update, delete) through `DaoRepository` resolved from a service registry, and (5) testing the same CRUD lifecycle through `DefaultDaoRepository` including child collections and cross-reference (xref) relationships.

The project includes three test data classes (`TestPerson`, `TestCar`, `TestAnimal`) that model parent-child (person owns cars) and many-to-many xref (person has pets, animal has owners) relationships. A `DaoRepoGenerationConfig.yaml` and `dao-repo-gen.json` configuration file are copied to the output directory to support schema repository generation tests.

## Key Classes

| Class | Description |
|---|---|
| `HandlebarsDaoCodeWriterShould` | Verifies that `HandlebarsCSharpDaoCodeWriter.Load()` calls `Reload()` on both `IHandlebarsDirectory` and `IHandlebarsEmbeddedResources` (uses NSubstitute mocks). |
| `SchemaProviderShould` | Tests that `SchemaProvider.CreateTypeSchema()` produces a `TypeSchema` with the correct name and expected number of tables (3) for the `TestPerson` graph. |
| `DefaultSchemaRepositoryGeneratorShould` | Integration test: loads `DaoRepoGenerationConfig.yaml`, creates a `HandlebarsSchemaRepositoryGenerator`, and calls `GenerateSource()` to produce DAO source files. |
| `DefaultDaoRepositoryShould` | Tests CRUD (create, retrieve, update, delete), child collection persistence, and xref persistence using `DefaultDaoRepository`. |
| `DaoRepositoryShould` | Tests CRUD, child collection persistence, and xref persistence using `DaoRepository` resolved from a manually configured `ServiceRegistry`. Also includes a `RuntimeSettingsTempTest` for reference assembly resolution. |
| `TestPerson` | Test POCO with scalar properties, a `List<TestCar>` child collection, and a `List<TestAnimal>` xref collection (`Pets`). |
| `TestCar` | Test POCO representing a child entity with a `TestPersonId` foreign key. |
| `TestAnimal` | Test POCO representing an xref entity with a `List<TestPerson>` (`Owners`) back-reference. |

## Dependencies

### Project References

- `bam.application`
- `bam.base`
- `bam.configuration`
- `bam.console`
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
- `bam.shell`
- `bam.test`
- `bam.generators`

### Package References

- `NSubstitute` 5.3.0

### Target Framework

- `net10.0` (Exe)

## Usage Examples

### Running All Unit Tests

```bash
dotnet run --project bam.generators.tests.csproj -- --ut
```

**Important:** Use `--ut` (double dash), not `/ut`. Git Bash on Windows rewrites `/ut` to a file path.

### Running a Specific Test Menu by Selector

```bash
# Run only SchemaProvider tests
dotnet run --project bam.generators.tests.csproj -- --ut sgs

# Run only DefaultDaoRepository tests
dotnet run --project bam.generators.tests.csproj -- --ut ddrt

# Run only DaoRepository tests
dotnet run --project bam.generators.tests.csproj -- --ut drt
```

### Test Data Class Relationships

```
TestPerson
  |-- TestCars (List<TestCar>)    -- parent/child via TestPersonId FK
  |-- Pets (List<TestAnimal>)     -- many-to-many xref

TestAnimal
  |-- Owners (List<TestPerson>)   -- many-to-many xref (reverse side)

TestCar
  |-- TestPerson                  -- FK navigation back to parent
```

## Configuration Files

- `DaoRepoGenerationConfig.yaml` -- Configures `HandlebarsSchemaRepositoryGenerator` with `FromNamespace: Bam.Generators.Tests.TestClasses`, `ToNamespace: Bam.Generators.Tests.TestClasses.Dao`, and `WriteSourceTo: ./Generated_Dao`.
- `dao-repo-gen.json` -- JSON variant of the generation config (copied to output directory).

## Known Gaps / Not Yet Implemented

No `NotImplementedException`, `TODO`, or stub methods were found in the test source code.
