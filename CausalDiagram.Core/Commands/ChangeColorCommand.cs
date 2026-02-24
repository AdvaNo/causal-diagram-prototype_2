using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    public class ChangeColorCommand : ICommand
    {
        private readonly List<Node> _nodes;
        private readonly List<NodeColor> _oldColors;
        private readonly NodeColor _newColor;

        public ChangeColorCommand(IEnumerable<Node> nodes, NodeColor newColor)
        {
            _nodes = nodes.ToList();
            _newColor = newColor;
            // Запоминаем старые цвета каждого узла
            _oldColors = _nodes.Select(n => n.ColorName).ToList();
        }

        public void Execute()
        {
            foreach (var node in _nodes)
            {
                node.ColorName = _newColor;
            }
        }

        public void Undo()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].ColorName = _oldColors[i];
            }
        }
    }
}
