using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    public class RenameNodeCommand : ICommand
    {
        private readonly Node _node;
        private readonly string _oldTitle;
        private readonly string _newTitle;

        public RenameNodeCommand(Node node, string newTitle)
        {
            _node = node;
            _oldTitle = node.Title;
            _newTitle = newTitle;
        }

        public void Execute() => _node.Title = _newTitle;
        public void Undo() => _node.Title = _oldTitle;
    }
}
