using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    public class AddNodeCommand : ICommand
    {
        private readonly Diagram _diagram;
        private readonly Node _node;

        public AddNodeCommand(Diagram diagram, Node node)
        {
            _diagram = diagram;
            _node = node;
        }

        public void Execute() => _diagram.Nodes.Add(_node);
        public void Undo() => _diagram.Nodes.Remove(_node);
    }

    public class RemoveNodeCommand : ICommand
    {
        private readonly Diagram _diagram;
        private readonly Node _node;
        private readonly List<Edge> _removedEdges = new List<Edge>();

        public RemoveNodeCommand(Diagram diagram, Node node)
        {
            _diagram = diagram;
            _node = node;
        }

        public void Execute()
        {
            _removedEdges.AddRange(_diagram.Edges.FindAll(e => e.From == _node.Id || e.To == _node.Id));
            foreach (var e in _removedEdges) _diagram.Edges.Remove(e);
            _diagram.Nodes.Remove(_node);
        }

        public void Undo()
        {
            _diagram.Nodes.Add(_node);
            _diagram.Edges.AddRange(_removedEdges);
            _removedEdges.Clear();
        }
    }
}
