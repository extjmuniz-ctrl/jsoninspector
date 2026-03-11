using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JsonInspector.Models
{
    public class PatronBusquedaSp
    {
        public string Descripcion { get; set; } = string.Empty;
        public List<string> VariantesExactas { get; set; } = new();
        public Regex Regex { get; set; } = null!;
    }
}
