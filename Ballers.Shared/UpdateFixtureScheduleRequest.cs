using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ballers.Shared
{
    public class UpdateFixtureScheduleRequest
    {
        public string Location { get; set; } = "";
        public DateTime KickOffTime { get;set;}
    }
}
