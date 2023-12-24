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
        public ulong Id { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }

        public ulong TestPersonId { get; set; }

        public virtual TestPerson TestPerson { get; set; }
    }
}
