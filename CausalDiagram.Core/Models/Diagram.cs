using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CausalDiagram.Core.Models
{
    public class Diagram
    {
        public List<Node> Nodes { get; set; } = new List<Node>();
        public List<Edge> Edges { get; set; } = new List<Edge>();

        // добавить правила категоризации в модель, чтобы сохранять/загружать вместе с файлом
        public List<ForbiddenRule> ForbiddenRules { get; set; } = new List<ForbiddenRule>();

        public void AddNode(Node node) => Nodes.Add(node);

        public bool TryConnect(Guid fromId, Guid toId)
        {
            // Базовая проверка: нельзя соединить узел с самим собой
            if (fromId == toId) return false;

            Edges.Add(new Edge { From = fromId, To = toId });
            return true;
        }
    }
}
