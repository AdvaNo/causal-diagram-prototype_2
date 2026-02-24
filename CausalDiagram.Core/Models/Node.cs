using System;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace CausalDiagram.Core.Models
{
    public class Node
    {
        [Browsable(false)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("Название")]
        [Description("Текст, который отображается внутри фактора")]
        public string Title { get; set; } = "Узел";

        [DisplayName("Описание")]
        [Description("Дополнительная заметка к фактору.")]
        public string Description { get; set; } = "";

        [Browsable(false)]
        public float X { get; set; }
        [Browsable(false)]
        public float Y { get; set; }
        [Browsable(false)]
        public float Weight { get; set; } = 0f;

        [Browsable(false)]
        // [DisplayName("...")] меняет название в таблице
        // [Description("...")] добавляет описание внизу 
        public NodeCategory Category { get; set; } = NodeCategory.Компонент;


        // Цвет и форма
        [DisplayName("Цвет")]
        [Description("Цвет заливки узла")]
        public NodeColor ColorName { get; set; } = NodeColor.Green;

        //[JsonIgnore]
        //public int Rpn => Severity * Occurrence * Detectability;

        [Browsable(false)]
        [JsonIgnore] // чтобы не сериализовать визуальные состояния
        public bool IsHighlighted { get; set; } = false;



        // [DisplayName("...")] меняет название в таблице
        // [Description("...")] добавляет описание внизу 
        public Node Clone()
        {
            return new Node
            {
                Id = this.Id, // Копируем текущий ID (в методе Paste мы его заменим)
                Title = this.Title,
                X = this.X,
                Y = this.Y,
                ColorName = this.ColorName
            };
        }
    }
}
