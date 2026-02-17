using System.Collections.Generic;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Commands
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

        public void Execute()
        {
            _diagram.Nodes.Add(_node);
        }

        public void Undo()
        {
            _diagram.Nodes.Remove(_node);
        }
    }
}