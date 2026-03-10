using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonInspector.Models
{
    public class Dashboard
    {
        public int DashboardNum { get; set; }
        public string DashboardName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JiraParentId { get; set; } = string.Empty;
        public List<DataProvider> DataProviders { get; set; } = new();
    }
}
