using System.Collections.Generic;
using CausalDiagram.Core.Models;

namespace CausalDiagram.Commands
{
    public class PasteCommand : ICommand
    {
        private readonly Diagram _diagram;
        private readonly List<Node> _pastedNodes;

        public PasteCommand(Diagram diagram, List<Node> nodesToPaste)
        {
            _diagram = diagram;
            // Сохраняем ссылки на конкретные экземпляры, которые мы вставили
            _pastedNodes = nodesToPaste;
        }

        public void Execute()
        {
            foreach (var node in _pastedNodes)
            {
                _diagram.Nodes.Add(node);
            }
        }

        public void Undo()
        {
            foreach (var node in _pastedNodes)
            {
                _diagram.Nodes.Remove(node);
            }
        }
    }
}