using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Ellipsis.Api
{
    public static class Settings { 
        public static string Version { get; set; }
        public static string ApiUrl { get; set; }

        public static void Initialize()
        {
            Version = "1.4";
            ApiUrl = "https://api.ellipsis-drive.com";
            //ApiUrl = "https://api..ellipsis-drive.com";
        }
    }
}
