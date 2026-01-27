using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CausalDiagram.Core.Models
{
    public class ForbiddenRule
    {
        public NodeCategory FromCategory { get; set; }
        public NodeCategory ToCategory { get; set; }
        public string Reason { get; set; } = "";

        public override string ToString()
        {
            return $"{FromCategory} → {ToCategory}" + (string.IsNullOrWhiteSpace(Reason) ? "" : $": {Reason}");
        }
    }
}
