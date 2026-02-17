using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CausalDiagram.Core.Models;
using CausalDiagram.Commands;

namespace CausalDiagram.Core.Commands
{
    // смена цвета узла с поддержкой вернуть/отменить
    public class ChangeNodeColorCommand : ICommand
    {
        private readonly Node _node;
        private readonly NodeColor _oldColor;
        private readonly NodeColor _newColor;

        public ChangeNodeColorCommand(Node node, NodeColor newColor)
        {
            _node = node;
            _oldColor = node.ColorName;
            _newColor = newColor;
        }

        public void Execute()
        {
            _node.ColorName = _newColor;
        }

        public void Undo()
        {
            _node.ColorName = _oldColor;
        }
    }

    // командa редактирования свойств узла — сохраняет старое и новое состояние узла
    public class EditNodePropertiesCommand : ICommand
    {
        private readonly Node _node;
        private readonly Node _oldSnapshot;
        private readonly Node _newSnapshot;

        public EditNodePropertiesCommand(Node node, Node oldSnapshot, Node newSnapshot)
        {
            _node = node;
            _oldSnapshot = oldSnapshot;
            _newSnapshot = newSnapshot;
        }

        public void Execute()
        {
            Apply(_newSnapshot);
        }

        public void Undo()
        {
            Apply(_oldSnapshot);
        }

        private void Apply(Node s)
        {

            _node.Title = s.Title;
            _node.Description = s.Description;
            _node.Weight = s.Weight;
            _node.ColorName = s.ColorName;
            _node.Category = s.Category;
        }
    }
}
