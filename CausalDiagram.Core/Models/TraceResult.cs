using System;
using System.Collections.Generic;

namespace CausalDiagram.Core.Models // Убедись, что namespace совпадает с твоим проектом
{
    /// <summary>
    /// Класс для хранения результатов анализа первопричин (Root Cause Analysis).
    /// </summary>
    public class TraceResult
    {
        /// <summary>
        /// Узел-результат, для которого мы запускали поиск (от которого шли назад).
        /// </summary>
        public Node TargetNode { get; set; }

        /// <summary>
        /// Список найденных ПЕРВОПРИЧИН (узлы, в которые не входят стрелки).
        /// По условию: именно по ним мы считаем "количество причин".
        /// </summary>
        public List<Node> RootCauses { get; set; } = new List<Node>();

        /// <summary>
        /// ID всех связей (стрелок), которые привели от первопричин к результату.
        /// Используем HashSet для мгновенной проверки при отрисовке подсветки.
        /// </summary>
        public HashSet<Guid> PathEdgeIds { get; set; } = new HashSet<Guid>();

        /// <summary>
        /// ID всех узлов, участвующих в цепочках (включая промежуточные).
        /// Пригодится, если захочешь подсветить не только стрелки, но и сами узлы на пути.
        /// </summary>
        public HashSet<Guid> PathNodeIds { get; set; } = new HashSet<Guid>();

        /// <summary>
        /// Текстовые описания всех найденных путей.
        /// Например: "Нехватка бюджета -> Срыв сроков -> Падение продаж"
        /// </summary>
        public List<string> PathDescriptions { get; set; } = new List<string>();

        /// <summary>
        /// Флаг: замкнут ли граф в бесконечный цикл на этом пути.
        /// Защитит программу от зависания и предупредит пользователя.
        /// </summary>
        public bool HasCycle { get; set; } = false;

        /// <summary>
        /// Удобное свойство-помощник: есть ли вообще найденные первопричины.
        /// </summary>
        public bool HasCauses => RootCauses.Count > 0;

        /// <summary>
        /// Конструктор
        /// </summary>
        public TraceResult(Node target)
        {
            TargetNode = target;
            // Сразу добавляем целевой узел в список подсвечиваемых
            if (target != null)
            {
                PathNodeIds.Add(target.Id);
            }
        }
    }
}
