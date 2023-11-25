﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Generators.Tests.TestClasses
{
    public class TestPerson
    {
        public TestPerson() { }

        public string Name { get; set; }

        public ulong Id { get; set; }

        public int IntProperty { get; set; }
        public uint UIntProperty { get; set; }
        public bool BooleanProperty { get; set; }
        public ulong ULongProperty { get; set; }
        public long LongProperty { get; set; }
        public decimal DecimalProperty { get; set; }
        public byte[] ByteArrayProperty { get; set; }
        public DateTime? DateTimeProperty { get; set; }

        public List<TestAnimal> Pets { get; set; }

        public List<TestCar> TestCars { get; set; }
    }
}
