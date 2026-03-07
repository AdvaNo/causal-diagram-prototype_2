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

        // Флаг: были ли изменения с момента последнего сохранения
        public bool IsModified { get; private set; }

        public void Execute(ICommand command)
        {
            command.Execute();
            _undo.Push(command);
            _redo.Clear();
            IsModified = true;
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Undo()
        {
            if (_undo.Count > 0)
            {
                var command = _undo.Pop();
                command.Undo();
                _redo.Push(command);
                IsModified = true;
            }
        }

        public void Redo()
        {
            if (_redo.Count > 0)
            {
                var command = _redo.Pop();
                command.Execute();
                _undo.Push(command);
                IsModified = true;
            }
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            IsModified = false;
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

        //сброс флага после сохранения
        public void ResetModified()
        {
            IsModified = false;
        }
    }
}
