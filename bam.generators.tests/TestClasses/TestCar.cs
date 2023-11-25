using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators.Tests.TestClasses
{
    public class TestCar
    {
        public TestCar() { }
        public string Make { get; set; }
        public string Model { get; set; }

        public ulong TestPersonId { get; set; }

        public TestPerson TestPerson { get; set; }
    }
}
