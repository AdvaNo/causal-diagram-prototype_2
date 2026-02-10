using System;
using System.IO;
using Newtonsoft.Json;
using CausalDiagram.Core.Models; // Убедись, что этот namespace совпадает с твоим
using CausalDiagram.Core.Commands;


namespace CausalDiagram.Services
{
    public class ProjectService
    {
        // Настройка сериализатора для корректной работы с Enum и форматированием
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto, // Чтобы сохранять типы наследников
            Formatting = Formatting.Indented,         // Красивый JSON
            NullValueHandling = NullValueHandling.Ignore
        };

        public void Save(Diagram diagram, string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(diagram, _settings);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new IOException($"Ошибка при сохранении файла: {ex.Message}", ex);
            }
        }

        public Diagram Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл не найден", filePath);

            try
            {
                var json = File.ReadAllText(filePath);
                var diagram = JsonConvert.DeserializeObject<Diagram>(json, _settings);
                return diagram ?? new Diagram();
            }
            catch (Exception ex)
            {
                throw new IOException($"Ошибка при чтении файла (возможно, формат устарел): {ex.Message}", ex);
            }
        }
    }
}