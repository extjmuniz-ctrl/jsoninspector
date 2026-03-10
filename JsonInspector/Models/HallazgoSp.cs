using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonInspector.Models
{
    public class HallazgoSp
    {
        public int DashboardNum { get; set; }
        public string DashboardName { get; set; } = string.Empty;
        public string Archivo { get; set; } = string.Empty;
        public int DataProviderId { get; set; }
        public string DataProviderName { get; set; } = string.Empty;
        public List<Coincidencia> Coincidencias { get; set; } = new();
    }
}
