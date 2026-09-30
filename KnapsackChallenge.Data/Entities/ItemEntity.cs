using System;
using System.Collections.Generic;
using System.Text;

namespace KnapsackChallenge.Data.Entities
{
    internal class ItemEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Weight { get; set; }
        public int Value { get; set; }
    }
}
