using System;
using System.Collections.Generic;
using System.Text;

namespace SaviAccess
{
    class AppConfiguration
    {
        public Dictionary<string, string> ConnectionStrings { get; set; }
        public GeneralSettings General { get; set; }
    }
}
