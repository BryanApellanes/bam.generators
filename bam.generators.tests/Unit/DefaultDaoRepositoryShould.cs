using Bam.Generators.Tests.TestClasses;
using Bam.Data.Repositories;
using Bam.DependencyInjection;
using Bam.Test;
using Bam.Console;
using Bam.Services;

namespace Bam.Generators.Tests.Unit
{
    [UnitTestMenu("DefaultDaoRepository Should", Selector = "ddrt")]
    public class DefaultDaoRepositoryShould : UnitTestMenuContainer
    {
        public DefaultDaoRepositoryShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        [ConsoleCommand("DefaultDaoRepositoryShould Create Entry")]
        [UnitTest]
        public void CreateEntry()
        {
            string testName = 32.RandomLetters();

            When.A<IDaoRepository>("creates a TestPerson entry",
                () => new DefaultDaoRepository(),
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

        [ConsoleCommand("DefaultDaoRepositoryShould Retrieve Entry")]
        [UnitTest]
        public void RetrieveEntry()
        {
            string testName = 32.RandomLetters();

            When.A<IDaoRepository>("creates and retrieves a TestPerson entry",
                () => new DefaultDaoRepository(),
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

        [ConsoleCommand("DefaultDaoRepositoryShould Update Entry")]
        [UnitTest]
        public void UpdateEntry()
        {
            string testName = 32.RandomLetters();
            string updatedName = 16.RandomLetters();

            When.A<IDaoRepository>("creates, retrieves, and updates a TestPerson entry",
                () => new DefaultDaoRepository(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;
                    TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
                    object? createException = repo.LastException;

                    TestPerson retrievedPerson = repo.Retrieve<TestPerson>(testPerson.Id)!;
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

        [ConsoleCommand("DefaultDaoRepositoryShould Delete Entry")]
        [UnitTest]
        public void DeleteEntry()
        {
            string testName = 32.RandomLetters();

            When.A<IDaoRepository>("creates, deletes, and verifies deletion of a TestPerson entry",
                () => new DefaultDaoRepository(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;
                    TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
                    object? createException = repo.LastException;

                    TestPerson? retrievedPerson = repo.Retrieve<TestPerson>(testPerson.Id);
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
                because.ItsTrue("retrieved is not null", retrievedPerson != null);
                because.ItsTrue("retrieved Id equals created Id", retrievedPerson!.Id == testPerson.Id);
                because.ItsTrue("delete returned true", deleted);
                because.ItsTrue("re-retrieve returns null after delete", results[5] == null);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [ConsoleCommand("DefaultDaoRepositoryShould Save Children")]
        [UnitTest]
        public void SaveChildren()
        {
            string testName = 32.RandomLetters();

            When.A<IDaoRepository>("creates a TestPerson with children and retrieves them",
                () => new DefaultDaoRepository(),
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

        [ConsoleCommand("DefaultDaoRepositoryShould Save Xrefs")]
        [UnitTest]
        public void SaveXrefs()
        {
            string testPersonName = 32.RandomLetters();
            string testAnimalName = 16.RandomLetters();

            When.A<IDaoRepository>("creates a TestPerson with xrefs and retrieves them",
                () => new DefaultDaoRepository(),
                (repo) =>
                {
                    repo.AddType(typeof(TestPerson));
                    object? addTypeException = repo.LastException;

                    TestPerson testPerson = new TestPerson { Name = testPersonName };
                    testPerson.Pets.Add(new TestAnimal { Name = testAnimalName });

                    testPerson = repo.Create(testPerson);
                    TestPerson retrieved = repo.Retrieve<TestPerson>(testPerson.Id)!;
                    TestAnimal pet = repo.Retrieve<TestAnimal>(retrieved!.Pets[0].Id)!;

                    return new object?[] { addTypeException, testPerson, retrieved, pet };
                })
            .TheTest
            .ShouldPass(because =>
            {
                object?[] results = (object?[])because.Result;
                because.ItsTrue("no exception on AddType", results[0] == null);
                TestPerson testPerson = (TestPerson)results[1]!;
                TestPerson retrieved = (TestPerson)results[2]!;
                TestAnimal pet = (TestAnimal)results[3]!;
                because.ItsTrue("created Id is greater than 0", testPerson.Id > 0);
                because.ItsTrue("retrieved is not null", retrieved != null);
                because.ItsTrue("retrieved name equals test name", testPersonName.Equals(retrieved!.Name));
                because.ItsTrue("retrieved has 1 pet", retrieved.Pets.Count == 1);
                because.ItsTrue("pet Id is greater than 0", retrieved.Pets[0].Id > 0);
                because.ItsTrue("pet is not null", pet != null);
                because.ItsTrue("pet name equals test animal name", testAnimalName.Equals(pet!.Name));
                because.ItsTrue("pet has 1 owner", pet.Owners.Count == 1);
                because.ItsTrue("pet owner Id equals retrieved person Id", pet.Owners[0].Id == retrieved.Id);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }
    }
}
