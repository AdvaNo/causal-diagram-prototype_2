using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using CausalDiagram.Core.Models;
using Newtonsoft.Json;

namespace CausalDiagram.Core
{
    // Класс-контейнер для сохранения всего состояния диаграммы
    public class DiagramData
    {
        public List<Node> Nodes { get; set; } = new List<Node>();
        public List<Edge> Edges { get; set; } = new List<Edge>();
    }

    public static class DiagramSerializer
    {
        // Настройки: чтобы JSON был читаемым (с отступами)
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static void SaveToFile(string filePath, Diagram diagram)
        {
            var data = new DiagramData
            {
                Nodes = diagram.Nodes,
                Edges = diagram.Edges
            };

            string jsonString = System.Text.Json.JsonSerializer.Serialize(data, _options);
            File.WriteAllText(filePath, jsonString);
        }

        public static Diagram LoadFromFile(string filePath)
        {
            string jsonString = File.ReadAllText(filePath);
            var data = System.Text.Json.JsonSerializer.Deserialize<DiagramData>(jsonString, _options);

            var diagram = new Diagram();
            if (data != null)
            {
                // Заполняем диаграмму данными, GUID'ы System.Text.Json подхватит автоматически
                diagram.Nodes.AddRange(data.Nodes);
                diagram.Edges.AddRange(data.Edges);
            }
            return diagram;
        }
    }
}
