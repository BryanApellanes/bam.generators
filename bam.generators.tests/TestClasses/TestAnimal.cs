using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators.Tests.TestClasses
{
    public class TestAnimal
    {
        public TestAnimal() { }

        public string Name { get; set; }

        public List<TestPerson> Owners { get; set; }
    }
}
