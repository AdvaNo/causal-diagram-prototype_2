using System;
using System.Collections.Generic;
using System.Linq;
using CausalDiagram.Core.Models;

namespace CausalDiagram.Core.Services
{
    public static class TraceAnalyzer
    {
        /// <summary>
        /// Главный метод для запуска трассировки причин.
        /// </summary>
        /// <param name="diagram">Текущая диаграмма (содержит все узлы и связи)</param>
        /// <param name="targetNode">Узел-результат, для которого ищем причины</param>
        /// <returns>Объект TraceResult со всеми путями и списком первопричин</returns>
        public static TraceResult Analyze(Diagram diagram, Node targetNode)
        {
            var result = new TraceResult(targetNode);

            if (diagram == null || targetNode == null)
                return result;

            // 1. ОПТИМИЗАЦИЯ: Создаем словари для мгновенного поиска
            // Это ускорит работу в сотни раз на больших схемах.
            // Словарь узлов по их ID:
            var nodesMap = diagram.Nodes.ToDictionary(n => n.Id);
            
            // Словарь входящих связей: ключ - To (куда входит), значение - список связей
            var incomingEdgesMap = diagram.Edges
                .GroupBy(e => e.To)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Этот HashSet будет хранить ID узлов в ТЕКУЩЕЙ ветке для защиты от циклов
            var currentPathVisited = new HashSet<Guid>();

            // 2. ВНУТРЕННЯЯ РЕКУРСИВНАЯ ФУНКЦИЯ (Идет задом наперед)
            void TraverseBackwards(Node currentNode, List<Node> pathNodes, List<Guid> pathEdges)
            {
                // ЗАЩИТА ОТ ЦИКЛОВ: Если мы уже заходили в этот узел в рамках ТЕКУЩЕГО пути
                if (currentPathVisited.Contains(currentNode.Id))
                {
                    result.HasCycle = true;
                    return; // Прерываем эту ветку
                }

                // Помечаем, что зашли в узел
                currentPathVisited.Add(currentNode.Id);
                result.PathNodeIds.Add(currentNode.Id);

                // Добавляем все связи из текущего пути в общий котел для подсветки
                foreach (var edgeId in pathEdges)
                {
                    result.PathEdgeIds.Add(edgeId);
                }

                // 3. ПРОВЕРЯЕМ: Есть ли входящие стрелки в этот узел?
                if (!incomingEdgesMap.TryGetValue(currentNode.Id, out var incomingEdges) || incomingEdges.Count == 0)
                {
                    // ВХОДЯЩИХ СВЯЗЕЙ НЕТ! МЫ НАШЛИ ПЕРВОПРИЧИНУ!
                    
                    // Добавляем в список причин (если её там еще нет)
                    if (!result.RootCauses.Any(r => r.Id == currentNode.Id))
                    {
                        result.RootCauses.Add(currentNode);
                    }

                    // Формируем красивую текстовую цепочку
                    // Так как мы шли с конца, список узлов нужно перевернуть (Reverse)
                    var pathNames = pathNodes.Select(n => n.Title).Reverse();
                    string textPath = string.Join(" ➔ ", pathNames);
                    result.PathDescriptions.Add(textPath);
                }
                else
                {
                    // ВХОДЯЩИЕ СВЯЗИ ЕСТЬ! Идем дальше "против течения"
                    foreach (var edge in incomingEdges)
                    {
                        // Находим узел, из которого вышла эта стрелка
                        if (nodesMap.TryGetValue(edge.From, out var prevNode))
                        {
                            // "Спускаемся" на уровень глубже
                            pathNodes.Add(prevNode);
                            pathEdges.Add(edge.Id);
                            
                            TraverseBackwards(prevNode, pathNodes, pathEdges);
                            
                            // "Поднимаемся" обратно (очищаем хвосты для других веток)
                            pathNodes.RemoveAt(pathNodes.Count - 1);
                            pathEdges.RemoveAt(pathEdges.Count - 1);
                        }
                    }
                }

                // Выходим из узла, снимаем пометку
                currentPathVisited.Remove(currentNode.Id);
            }

            // 4. ЗАПУСК РЕКУРСИИ
            // Начинаем с целевого узла
            TraverseBackwards(
                targetNode, 
                new List<Node> { targetNode }, // Начальный путь состоит только из результата
                new List<Guid>()               // Пока не прошли ни одной связи
            );

            return result;
        }
    }
}
