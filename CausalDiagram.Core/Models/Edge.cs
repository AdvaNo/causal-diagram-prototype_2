using System;
using Newtonsoft.Json;


namespace CausalDiagram.Core.Models
{
    public class Edge
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid From { get; set; }
        public Guid To { get; set; }

        [JsonIgnore]
        public bool IsHighlighted { get; set; } = false;

        //[JsonIgnore]
        public bool IsForbidden { get; set; } = false;
    }
}
