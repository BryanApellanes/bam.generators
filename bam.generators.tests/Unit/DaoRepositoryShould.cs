using Bam.Services;
using Bam.Test;
using Bam.Data.Repositories;
using Bam.Data.Schema;
using Bam.Generators.Tests.TestClasses;
using Bam.Console;
using Bam.DependencyInjection;

namespace Bam.Generators.Tests.Unit
{
    [UnitTestMenu("DaoRepository Should", Selector = "drt")]
    public class DaoRepositoryShould : UnitTestMenuContainer
    {
        public DaoRepositoryShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
            Configure(svcRegistry =>
            {
                svcRegistry
                    .For<IDaoCodeWriter>().Use<HandlebarsCSharpDaoCodeWriter>()
                    .For<ISchemaProvider>().Use<SchemaProvider>()
                    .For<IDaoGenerator>().Use<DaoGenerator>()
                    .For<IWrapperGenerator>().Use<HandlebarsWrapperGenerator>()
                    .For<IDaoRepository>().Use<DaoRepository>();
            });
        }

        [UnitTest]
        public void RuntimeSettingsTempTest()
        {
            When.A<string>("gets reference assemblies directory",
                () => RuntimeSettings.GetReferenceAssembliesDirectory(),
                (path) => path)
            .TheTest
            .ShouldPass(because =>
            {
                because.TheResult.IsNotNull();
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void BeOfTypeDaoRepsitory()
        {
            When.A<IDaoRepository>("is resolved from DI",
                () => Get<IDaoRepository>(),
                (repo) => repo)
            .TheTest
            .ShouldPass(because =>
            {
                because.ItsTrue("repo is of type DaoRepository", because.Result is DaoRepository);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [ConsoleCommand("DaoRepositoryShould Create Entry")]
        [UnitTest]
        public void CreateEntry()
        {
            string testName = 32.RandomLetters();

            When.A<IDaoRepository>("creates a TestPerson entry",
                () => Get<IDaoRepository>(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;
                    TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
                    object? createException = repo.LastException;
                    return new object?[] { addTypeException, createException, testPerson };
                })
            .TheTest
            .ShouldPass(because =>
            {
                object?[] results = (object?[])because.Result;
                because.ItsTrue("no exception on AddType", results[0] == null);
                because.ItsTrue("no exception on Create", results[1] == null);
                TestPerson testPerson = (TestPerson)results[2]!;
                because.ItsTrue("name equals test name", testName.Equals(testPerson.Name));
                because.ItsTrue("Id is greater than 0", testPerson.Id > 0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [ConsoleCommand("DaoRepositoryShould Retrieve Entry")]
        [UnitTest]
        public void RetrieveEntry()
        {
            string testName = 32.RandomLetters();

            When.A<IDaoRepository>("creates and retrieves a TestPerson entry",
                () => Get<IDaoRepository>(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;
                    TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
                    object? createException = repo.LastException;
                    TestPerson retrievedPerson = repo.Retrieve<TestPerson>(testPerson.Id)!;
                    return new object?[] { addTypeException, createException, retrievedPerson };
                })
            .TheTest
            .ShouldPass(because =>
            {
                object?[] results = (object?[])because.Result;
                because.ItsTrue("no exception on AddType", results[0] == null);
                because.ItsTrue("no exception on Create", results[1] == null);
                TestPerson? retrieved = (TestPerson?)results[2];
                because.ItsTrue("retrieved person is not null", retrieved != null);
                because.ItsTrue("retrieved name equals test name", testName.Equals(retrieved?.Name));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [ConsoleCommand("DaoRepositoryShould Update Entry")]
        [UnitTest]
        public void UpdateEntry()
        {
            string testName = 32.RandomLetters();
            string updatedName = 16.RandomLetters();

            When.A<IDaoRepository>("creates, retrieves, and updates a TestPerson entry",
                () => Get<IDaoRepository>(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;
                    TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
                    object? createException = repo.LastException;

                    TestPerson? retrievedPerson = repo.Retrieve<TestPerson>(testPerson.Id)!;
                    retrievedPerson!.Name = updatedName;
                    TestPerson updatedPerson = repo.Update(retrievedPerson)!;

                    return new object?[] { addTypeException, createException, testPerson, retrievedPerson, updatedPerson };
                })
            .TheTest
            .ShouldPass(because =>
            {
                object?[] results = (object?[])because.Result;
                because.ItsTrue("no exception on AddType", results[0] == null);
                because.ItsTrue("no exception on Create", results[1] == null);
                TestPerson testPerson = (TestPerson)results[2]!;
                TestPerson retrievedPerson = (TestPerson)results[3]!;
                TestPerson updatedPerson = (TestPerson)results[4]!;
                because.ItsTrue("created Id is greater than 0", testPerson.Id > 0);
                because.ItsTrue("created name equals test name", testName.Equals(testPerson.Name));
                because.ItsTrue("retrieved Id equals created Id", retrievedPerson.Id == testPerson.Id);
                because.ItsTrue("updated Id equals created Id", updatedPerson.Id == testPerson.Id);
                because.ItsTrue("updated name equals updated name", updatedName.Equals(updatedPerson.Name));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [ConsoleCommand("DaoRepositoryShould Delete Entry")]
        [UnitTest]
        public void DeleteEntry()
        {
            string testName = 32.RandomLetters();

            When.A<IDaoRepository>("creates, deletes, and verifies deletion of a TestPerson entry",
                () => Get<IDaoRepository>(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;
                    TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
                    object? createException = repo.LastException;

                    TestPerson retrievedPerson = repo.Retrieve<TestPerson>(testPerson.Id)!;
                    bool deleted = repo.Delete(retrievedPerson);
                    TestPerson? shouldBeNull = repo.Retrieve<TestPerson>(testPerson.Id);

                    return new object?[] { addTypeException, createException, testPerson, retrievedPerson, deleted, shouldBeNull };
                })
            .TheTest
            .ShouldPass(because =>
            {
                object?[] results = (object?[])because.Result;
                because.ItsTrue("no exception on AddType", results[0] == null);
                because.ItsTrue("no exception on Create", results[1] == null);
                TestPerson testPerson = (TestPerson)results[2]!;
                TestPerson retrievedPerson = (TestPerson)results[3]!;
                bool deleted = (bool)results[4]!;
                because.ItsTrue("created Id is greater than 0", testPerson.Id > 0);
                because.ItsTrue("created name equals test name", testName.Equals(testPerson.Name));
                because.ItsTrue("retrieved Id equals created Id", retrievedPerson.Id == testPerson.Id);
                because.ItsTrue("delete returned true", deleted);
                because.ItsTrue("re-retrieve returns null after delete", results[5] == null);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [ConsoleCommand("DaoRepositoryShould Save Children")]
        [UnitTest]
        public void SaveChildren()
        {
            string testName = 32.RandomLetters();

            When.A<IDaoRepository>("creates a TestPerson with children and retrieves them",
                () => Get<IDaoRepository>(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;

                    TestPerson testPerson = new TestPerson { Name = testName };
                    TestCar testCar = new TestCar
                    {
                        Make = 8.RandomLetters(),
                        Model = 8.RandomLetters()
                    };
                    testPerson.TestCars.Add(testCar);

                    testPerson = repo.Create(testPerson);
                    TestPerson retrieved = repo.Retrieve<TestPerson>(testPerson.Id)!;

                    return new object?[] { addTypeException, testPerson, retrieved };
                })
            .TheTest
            .ShouldPass(because =>
            {
                object?[] results = (object?[])because.Result;
                because.ItsTrue("no exception on AddType", results[0] == null);
                TestPerson testPerson = (TestPerson)results[1]!;
                TestPerson retrieved = (TestPerson)results[2]!;
                because.ItsTrue("created Id is greater than 0", testPerson.Id > 0);
                because.ItsTrue("retrieved Id equals created Id", retrieved.Id == testPerson.Id);
                because.ItsTrue("retrieved has 1 TestCar", retrieved.TestCars.Count == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [ConsoleCommand("DaoRepositoryShould Save Xrefs")]
        [UnitTest]
        public void SaveXrefs()
        {
            string testPersonName = 32.RandomLetters();
            string testAnimalName = 16.RandomLetters();

            When.A<IDaoRepository>("creates a TestPerson with xrefs and retrieves them",
                () => Get<IDaoRepository>(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;

                    TestPerson testPerson = new TestPerson { Name = testPersonName };
                    testPerson.Pets.Add(new TestAnimal { Name = testAnimalName });

                    testPerson = repo.Create(testPerson);
                    TestPerson retrieved = repo.Retrieve<TestPerson>(testPerson.Id)!;

                    return new object?[] { addTypeException, testPerson, retrieved };
                })
            .TheTest
            .ShouldPass(because =>
            {
                object?[] results = (object?[])because.Result;
                because.ItsTrue("no exception on AddType", results[0] == null);
                TestPerson testPerson = (TestPerson)results[1]!;
                TestPerson retrieved = (TestPerson)results[2]!;
                because.ItsTrue("created Id is greater than 0", testPerson.Id > 0);
                because.ItsTrue("retrieved has 1 pet", retrieved.Pets.Count == 1);
                because.ItsTrue("pet Id is greater than 0", retrieved.Pets[0].Id > 0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
