using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonInspector.Models
{
    public class DataProvider
    {
        public int DataProviderId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HelpText { get; set; } = string.Empty;
        public string DataSource { get; set; } = string.Empty;
        public QueryNode? Query { get; set; }
    }
}
