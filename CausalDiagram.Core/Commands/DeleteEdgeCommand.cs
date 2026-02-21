using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    public class DeleteEdgeCommand : ICommand
    {
        private readonly Diagram _diagram;
        private readonly Edge _edge;

        public DeleteEdgeCommand(Diagram diagram, Edge edge)
        {
            _diagram = diagram;
            _edge = edge;
        }

        public void Execute()
        {
            _diagram.Edges.Remove(_edge);
        }

        public void Undo()
        {
            _diagram.Edges.Add(_edge);
        }
    }
}
