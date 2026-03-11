using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonInspector.Models
{
    public class DashboardFileInfo
    {
        public Dashboard Dashboard { get; set; } = new Dashboard();
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool ContieneTextoCompleto { get; set; }
    }
}
