using System;
using System.Collections.Generic;
using System.Text;

namespace KnapsackChallenge.Data.Entities
{
    public class SelectedItemEntity
    {
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public int ItemId { get; set; }
    }
}
