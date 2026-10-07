using System;
using System.Collections.Generic;
using System.Text;

namespace KnapsackChallenge.Data.Entities
{
    public class RoomPlayerEntity
    {
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public int TotalScore { get; set; }
        public int TotalWeight { get; set; }
        public bool IsSubmitted { get; set; }
    }
}
