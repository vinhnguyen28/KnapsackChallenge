using System;
using System.Collections.Generic;
using System.Text;

namespace KnapsackChallenge.Data.Entities
{
    public class KnapsackSetEntity
    {
        public int Id { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int MaxWeight { get; set; }
        public string Difficulty { get; set; } = string.Empty; // 'Easy', 'Medium', 'Hard'
    }
}
