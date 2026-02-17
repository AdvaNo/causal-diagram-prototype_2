using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    namespace CausalDiagram.Commands
    {
        // Простой интерфейс без лишних аргументов
        public interface ICommand
        {
            void Execute(); // Скобки пустые!
            void Undo();    // Скобки пустые!
        }
    }

