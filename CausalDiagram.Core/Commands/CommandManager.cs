using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    
    public class CommandManager
    {
        private readonly Stack<ICommand> _undo = new Stack<ICommand>();
        private readonly Stack<ICommand> _redo = new Stack<ICommand>();

        public void Execute(ICommand command)
        {
            command.Execute();
            _undo.Push(command);
            _redo.Clear();
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Undo()
        {
        //    if (!CanUndo) return;
        //    var c = _undo.Pop();
        //    c.Undo();
        //    _redo.Push(c);
            if (_undo.Count > 0)
            {
                var command = _undo.Pop();
                command.Undo();
                _redo.Push(command);
            }
        }

        public void Redo()
        {
            //if (!CanRedo) return;
            //var c = _redo.Pop();
            //c.Execute();
            //_undo.Push(c);
            if (_redo.Count > 0)
            {
                var command = _redo.Pop();
                command.Execute();
                _undo.Push(command);
            }
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        /// <summary>
        /// Добавляет команду в стек отмены без её выполнения.
        /// Используется для действий, которые уже совершены пользователем в UI (например, перетаскивание).
        /// </summary>
        public void PushToUndoStack(ICommand command)
        {
            // Кладем готовую команду в стек Undo
            _undo.Push(command);

            // Как и при обычном Execute, очищаем стек Redo, 
            // так как ветка истории изменилась
            _redo.Clear();
        }
    }
}
