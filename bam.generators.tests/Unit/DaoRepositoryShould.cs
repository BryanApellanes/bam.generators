using Bam.Services;
using Bam.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bam.Net.CoreServices;
using Bam.Net.Data.Repositories;
using Bam.Net;
using Bam.Generators.Tests.TestClasses;
using Bam.Net.Data.Schema;
using Bam.Net.Data.SQLite;
using Bam.Console;
using Bam.Data.Schema;

namespace Bam.Generators.Tests.Unit
{
    [UnitTestMenu("DaoRepository Should", Selector = "drt")]
    public class DaoRepositoryShould : UnitTestMenuContainer
    {
        public DaoRepositoryShould(ServiceRegistry serviceRegistry) : base(serviceRegistry)
        {
        }

        public override ServiceRegistry Configure(ServiceRegistry serviceRegistry)
        {
            return serviceRegistry
                .For<IDaoCodeWriter>().Use<HandlebarsCSharpDaoCodeWriter>()
                .For<ISchemaProvider>().Use<SchemaProvider>()
                .For<IDaoGenerator>().Use<DaoGenerator>()
                .For<IWrapperGenerator>().Use<HandlebarsWrapperGenerator>()
                .For<IDaoRepository>().Use<DaoRepository>();
        }

        [UnitTest]
        public void RuntimeSettingsTempTest()
        {
            Message.PrintLine(RuntimeSettings.GetReferenceAssembliesDirectory());
        }

        [UnitTest]
        public void BeOfTypeDaoRepsitory()
        {
            IDaoRepository repo = Get<IDaoRepository>();
            repo.ShouldBeOfType<DaoRepository>();
        }

        [UnitTest]
        public void CreateEntry()
        {
            string testName = 32.RandomLetters();
            IDaoRepository repo = Get<IDaoRepository>();
            repo.AddType(typeof(TestPerson));
            repo.LastException.ShouldBeNull(repo.LastException?.Message);

            TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
            repo.LastException.ShouldBeNull(repo.LastException?.Message);
            testPerson.Name.ShouldBeEqualTo(testName);
            testPerson.Id.ShouldBeGreaterThan(0, $"Id should have been greater than 0 but was {testPerson.Id}");
        }

        [UnitTest]
        public void RetrieveEntry()
        {
            string testName = 32.RandomLetters();
            IDaoRepository repo = Get<IDaoRepository>();
            repo.AddType(typeof(TestPerson));
            repo.LastException.ShouldBeNull(repo.LastException?.Message);

            TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
            repo.LastException.ShouldBeNull(repo.LastException?.Message);

            TestPerson retrievedPerson = repo.Retrieve<TestPerson>(testPerson.Id);
            retrievedPerson.ShouldNotBeNull($"Unable to retrieve, result was null");
            retrievedPerson.Name.ShouldEqual(testName);
        }

        [UnitTest]
        public void UpdateEntry()
        {
            string testName = 32.RandomLetters();
            string updatedName = 16.RandomLetters();

            IDaoRepository repo = Get<IDaoRepository>();
            repo.AddType(typeof(TestPerson));
            repo.LastException.ShouldBeNull(repo.LastException?.Message);

            TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
            repo.LastException.ShouldBeNull(repo.LastException?.Message);
            testPerson.Id.ShouldBeGreaterThan(0);
            testPerson.Name.ShouldEqual(testName);

            TestPerson retrievedPerson = repo.Retrieve<TestPerson>(testPerson.Id);
            retrievedPerson.Id.ShouldEqual(testPerson.Id);
            retrievedPerson.Name = updatedName;

            TestPerson updatedPerson = repo.Update(retrievedPerson);
            updatedPerson.Id.ShouldEqual(testPerson.Id);
            updatedPerson.Name.ShouldEqual(updatedName);
        }

        [UnitTest]
        public void DeleteEntry()
        {
            string testName = 32.RandomLetters();

            IDaoRepository repo = Get<IDaoRepository>();
            repo.AddType(typeof(TestPerson));
            repo.LastException.ShouldBeNull(repo.LastException?.Message);

            TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
            repo.LastException.ShouldBeNull(repo.LastException?.Message);
            testPerson.Id.ShouldBeGreaterThan(0);
            testPerson.Name.ShouldEqual(testName);

            TestPerson retrievedPerson = repo.Retrieve<TestPerson>(testPerson.Id);
            retrievedPerson.Id.ShouldEqual(testPerson.Id);

            repo.Delete(retrievedPerson).IsTrue("failed to delete test data");

            TestPerson shouldBeNull = repo.Retrieve<TestPerson>(testPerson.Id);
            shouldBeNull.ShouldBeNull($"Expected to retrieve null but got data: {shouldBeNull?.ToJson()}");
        }

        [UnitTest]
        public void SaveChildren()
        {
            string testName = 32.RandomLetters();

            IDaoRepository repo = Get<IDaoRepository>();
            repo.AddType(typeof(TestPerson));
            repo.LastException.ShouldBeNull(repo.LastException?.Message);

            TestPerson testPerson = new TestPerson { Name = testName };
            TestCar testCar = new TestCar
            {
                Make = 8.RandomLetters(),
                Model = 8.RandomLetters()
            };
            testPerson.TestCars.Add(testCar);

            testPerson = repo.Create(testPerson);
            testPerson.Id.ShouldBeGreaterThan(0);

            TestPerson retrieved = repo.Retrieve<TestPerson>(testPerson.Id);
            retrieved.Id.ShouldEqual(testPerson.Id);
            retrieved.TestCars.Count.ShouldEqual(1);
        }

        [UnitTest]
        public void SaveXrefs()
        {
            string testPersonName = 32.RandomLetters();
            string testAnimalName = 16.RandomLetters();

            IDaoRepository repo = Get<IDaoRepository>();
            repo.AddType(typeof(TestPerson));
            repo.LastException.ShouldBeNull(repo.LastException?.Message);

            TestPerson testPerson = new TestPerson { Name = testPersonName };
            testPerson.Pets.Add(new TestAnimal { Name = testAnimalName });

            testPerson = repo.Create(testPerson);
            testPerson.Id.ShouldBeGreaterThan(0);

            TestPerson retrieved = repo.Retrieve<TestPerson>(testPerson.Id);
            retrieved.Pets.Count.ShouldEqual(1);
            retrieved.Pets[0].Id.ShouldBeGreaterThan(0);
        }
    }
}
