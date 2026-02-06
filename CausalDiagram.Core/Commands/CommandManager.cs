using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;

namespace CausalDiagram.Core.Commands
{
    public interface ICommand
    {
        void Execute();
        void Undo();
    }
    public class CommandManager
    {
        private readonly Stack<ICommand> _undo = new Stack<ICommand>();
        private readonly Stack<ICommand> _redo = new Stack<ICommand>();

        public void ExecuteCommand(ICommand c)
        {
            c.Execute();
            _undo.Push(c);
            _redo.Clear();
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Undo()
        {
            if (!CanUndo) return;
            var c = _undo.Pop();
            c.Undo();
            _redo.Push(c);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var c = _redo.Pop();
            c.Execute();
            _undo.Push(c);
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
