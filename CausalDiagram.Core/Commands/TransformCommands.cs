using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;

namespace CausalDiagram.Core.Commands
{
    public class MoveNodeCommand : ICommand
    {
        private readonly Node _node;
        private readonly float _oldX, _oldY;
        private readonly float _newX, _newY;

        public MoveNodeCommand(Node node, float oldX, float oldY, float newX, float newY)
        {
            _node = node;
            _oldX = oldX;
            _oldY = oldY;
            _newX = newX;
            _newY = newY;
        }

        public void Execute()
        {
            _node.X = _newX;
            _node.Y = _newY;
        }

        public void Undo()
        {
            _node.X = _oldX;
            _node.Y = _oldY;
        }
    }
}
