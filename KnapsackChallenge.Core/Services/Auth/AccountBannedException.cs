using System;

namespace KnapsackChallenge.Core.Services.Auth
{
    public class AccountBannedException : Exception
    {
        public AccountBannedException(string message) : base(message)
        {
        }
        
        public AccountBannedException(string message, Exception inner): base(message, inner)
        {
        }
    }
}
