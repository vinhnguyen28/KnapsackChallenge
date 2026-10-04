using System;
using System.Collections.Generic;
using System.Text;

namespace KnapsackChallenge.Data.Entities
{
    public class GameSessionEntity
    {
        public int Id { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // 'Waiting', 'Playing', 'Finished'
        public int SetId { get; set; }
    }
}