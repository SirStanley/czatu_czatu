using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CzatuCzatu.Models
{
    namespace CzatuCzatu.Models
    {
        public class User
        {
            public int Id { get; set; }
            public required string Username { get; set; }
            public required string PasswordHash { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
