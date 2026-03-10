using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonInspector.Models
{
    public class QueryNode
    {
        public int Tipo { get; set; }
        public string TipoDescripcion { get; set; } = string.Empty;
        public string SqlQuery { get; set; } = string.Empty;
        public List<Servidor> Servidores { get; set; } = new();
        public List<QueryNode> Children { get; set; } = new();
    }
}
