using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    public class PasteGroupCommand : ICommand
    {
        private readonly Diagram _diagram;
        private readonly List<Node> _nodes;
        private readonly List<Edge> _edges;

        public PasteGroupCommand(Diagram diagram, List<Node> nodes, List<Edge> edges)
        {
            _diagram = diagram;
            _nodes = nodes;
            _edges = edges;
        }

        public void Execute()
        {
            _diagram.Nodes.AddRange(_nodes);
            _diagram.Edges.AddRange(_edges);
        }

        public void Undo()
        {
            foreach (var n in _nodes) _diagram.Nodes.Remove(n);
            foreach (var e in _edges) _diagram.Edges.Remove(e);
        }
    }
}
