using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonInspector.Models
{
    public class Servidor
    {
        public string Nombre { get; set; } = string.Empty;
        public List<BaseDatos> BaseDatos { get; set; } = new();
    }
}
