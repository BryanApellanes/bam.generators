using Bam.Services;
using Bam.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bam.Net.CoreServices;
using Bam.Net.CommandLine;
using Bam.Net.Data.Repositories;
using Bam.Net;
using Bam.Generators.Tests.TestClasses;
using Bam.Net.Data.Schema;
using Bam.Net.Data.Schema.Handlebars;

namespace Bam.Generators.Tests
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
                .For<ISchemaGenerator>().Use<SchemaGenerator>()
                .For<IDaoGenerator>().Use<DaoGenerator>()
                .For<IWrapperGenerator>().Use<TemplatedWrapperGenerator>()
                .For<IDaoRepository>().Use<DaoRepository>();
        }

        [UnitTest]
        public void BeOfTypeDaoRepsitory()
        {
            IDaoRepository repo = this.Get<IDaoRepository>();
            repo.ShouldBeOfType<DaoRepository>();
        }

        [UnitTest]
        public void CreateEntry()
        {
            string testName = 32.RandomLetters();
            IDaoRepository repo = this.Get<IDaoRepository>();
            repo.AddType(typeof(TestPerson));

            TestPerson testPerson = repo.Create(new TestPerson { Name = testName });
            testPerson.Name.ShouldBeEqualTo(testName);
            Expect.IsGreaterThan(testPerson.Id, 0, $"Id should have been greater than 0 but was {testPerson.Id}");
        }

        [UnitTest]
        public void RetrieveEntry()
        {
            throw new NotImplementedException("This test is not complete");
        }

        [UnitTest]
        public void UpdateEntry()
        {
            throw new NotImplementedException("This test is not complete");
        }

        [UnitTest]
        public void DeleteEntry()
        {
            throw new NotImplementedException("This test is not complete");
        }

        [UnitTest]
        public void DeleteMe()
        {
            Message.PrintLine("This should be green", ConsoleColor.Green);
        }
    }
}
