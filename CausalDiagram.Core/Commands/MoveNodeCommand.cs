using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;

namespace CausalDiagram.Commands
{
    public class MoveNodeCommand : ICommand
    {
        // Запоминаем список узлов и их старые/новые координаты
        private readonly List<(Node node, float oldX, float oldY, float newX, float newY)> _moves;

        public MoveNodeCommand(List<(Node node, float oldX, float oldY, float newX, float newY)> moves)
        {
            _moves = new List<(Node, float, float, float, float)>(moves);
        }

        public void Execute()
        {
            foreach (var m in _moves)
            {
                m.node.X = m.newX;
                m.node.Y = m.newY;
            }
        }

        public void Undo()
        {
            foreach (var m in _moves)
            {
                m.node.X = m.oldX;
                m.node.Y = m.oldY;
            }
        }
    }
}
