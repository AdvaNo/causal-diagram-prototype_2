using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    public class AddEdgeCommand : ICommand
    {
        private readonly Diagram _diagram;
        private readonly Edge _edge;

        public AddEdgeCommand(Diagram diagram, Edge edge)
        {
            _diagram = diagram;
            _edge = edge;
        }

        public void Execute() => _diagram.Edges.Add(_edge);
        public void Undo() => _diagram.Edges.Remove(_edge);
    }

    public class RemoveEdgeCommand : ICommand
    {
        private readonly Diagram _diagram;
        private readonly Edge _edge;

        public RemoveEdgeCommand(Diagram diagram, Edge edge)
        {
            _diagram = diagram;
            //сам объект
            _edge = edge;
            //копия ребра
            //_edge = new Edge { Id = edge.Id, From = edge.From, To = edge.To };
        }

        public void Execute()
        {
            //именно этот объект
            _diagram.Edges.Remove(_edge);
            //var existing = _diagram.Edges.FirstOrDefault(e => e.Id == _edge.Id);
            //if (existing != null) _diagram.Edges.Remove(existing);
        }

        public void Undo()
        {
            // восстановим, если не существует
            if (!_diagram.Edges.Contains(_edge))
            {
                _diagram.Edges.Add(_edge);
            }
            //if (!_diagram.Edges.Any(e => e.Id == _edge.Id))
            //{
            //    _diagram.Edges.Add(_edge);
            //}
        }
    }
}
