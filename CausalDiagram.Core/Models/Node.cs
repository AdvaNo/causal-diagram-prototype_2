using System;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CausalDiagram.Core.Models
{
    public class Node
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "Узел";
        public string Description { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Weight { get; set; } = 0f;
        public NodeCategory Category { get; set; } = NodeCategory.Компонент;


        // Цвет и форма
        public NodeColor ColorName { get; set; } = NodeColor.Green;

        //[JsonIgnore]
        //public int Rpn => Severity * Occurrence * Detectability;

        [JsonIgnore] // чтобы не сериализовать визуальные состояния
        public bool IsHighlighted { get; set; } = false;

    }
}
