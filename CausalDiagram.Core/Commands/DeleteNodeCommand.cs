using System;
using System.Collections.Generic;
using System.Linq;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    public class DeleteNodeCommand : ICommand
    {
        private readonly Diagram _diagram;
        private readonly List<Node> _removedNodes;
        private readonly List<Edge> _removedEdges;

        public DeleteNodeCommand(Diagram diagram, List<Guid> nodeIdsToDelete)
        {
            _diagram = diagram;

            // 1. Запоминаем сами объекты узлов перед тем, как они исчезнут
            _removedNodes = _diagram.Nodes.Where(n => nodeIdsToDelete.Contains(n.Id)).ToList();

            // 2. Запоминаем ВСЕ связи, которые касались этих узлов
            _removedEdges = _diagram.Edges.Where(e =>
                nodeIdsToDelete.Contains(e.From) || nodeIdsToDelete.Contains(e.To)).ToList();
        }

        public void Execute()
        {
            // Удаляем связи
            foreach (var edge in _removedEdges) _diagram.Edges.Remove(edge);
            // Удаляем узлы
            foreach (var node in _removedNodes) _diagram.Nodes.Remove(node);
        }

        public void Undo()
        {
            // Возвращаем всё в обратном порядке
            foreach (var node in _removedNodes) _diagram.Nodes.Add(node);
            foreach (var edge in _removedEdges) _diagram.Edges.Add(edge);
        }
    }
}