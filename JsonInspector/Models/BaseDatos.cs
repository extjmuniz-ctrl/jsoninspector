using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonInspector.Models
{
    public class BaseDatos
    {
        public string Nombre { get; set; } = string.Empty;
        public List<string> Tablas { get; set; } = new();
    }
}
