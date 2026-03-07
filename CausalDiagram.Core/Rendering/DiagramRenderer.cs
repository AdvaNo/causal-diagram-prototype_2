using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CausalDiagram.Core.Models;

namespace CausalDiagram.Rendering
{
    public class DiagramRenderer
    {
        // Настройки шрифтов
        private readonly Font _mainFont = new Font("Segoe UI", 10f);

        //Динамический расчет размера узла
        public SizeF CalculateNodeSize(Graphics g, Node node)
        {
            var minSize = new SizeF(120, 60); // Минимальный размер, как раньше
            if (string.IsNullOrEmpty(node.Title)) return minSize;

            // Измеряем текст с учетом отступов (padding = 10px)
            var textSize = g.MeasureString(node.Title, _mainFont);

            float width = Math.Max(minSize.Width, textSize.Width + 20);
            float height = Math.Max(minSize.Height, textSize.Height + 20);

            return new SizeF(width, height);
        }

        // Находит точку на границе прямоугольника rect, ближайшую к точке target
        private PointF GetRectBoundaryPointTowards(RectangleF rect, PointF target)
        {
            var center = new PointF(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            var dx = target.X - center.X;
            var dy = target.Y - center.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);

            // Если точки совпадают, возвращаем центр во избежание деления на ноль
            if (len < 0.0001f) return center;

            // Нормализованный вектор направления
            var ux = dx / len;
            var uy = dy / len;

            // Половины ширины и высоты
            float hx = rect.Width / 2f;
            float hy = rect.Height / 2f;

            // Вычисляем время t до пересечения с вертикальными и горизонтальными границами
            // Используем Math.Abs и деление, чтобы найти минимальное расстояние до границы
            float tx = Math.Abs(hx / ux);
            float ty = Math.Abs(hy / uy);

            // Выбираем меньшее t — это и есть первое пересечение с границей
            float t = Math.Min(tx, ty);

            return new PointF(center.X + ux * t, center.Y + uy * t);
        }


        // Возвращает готовый прямоугольник для узла
        public RectangleF GetNodeBounds(Graphics g, Node node)
        {
            var size = CalculateNodeSize(g, node);
            return new RectangleF(node.X - size.Width / 2f, node.Y - size.Height / 2f, size.Width, size.Height);
        }

        public void Render(Graphics g, Diagram diagram, HashSet<Guid> selectedNodeIds, Edge selectedEdge, float zoom, PointF panOffset, bool showGrid, int gridStep, TraceResult activeTrace)
        {
            // 1. Сначала рисуем все связи (чтобы они были под узлами)
            foreach (var edge in diagram.Edges)
            {
                // Находим узлы по ID из списка
                var fromNode = diagram.Nodes.Find(n => n.Id == edge.From);
                var toNode = diagram.Nodes.Find(n => n.Id == edge.To);

                if (fromNode != null && toNode != null)
                {
                    bool isEdgeSelected = (edge == selectedEdge);
                    DrawConnection(g, fromNode, toNode, selectedEdge, isEdgeSelected,zoom, activeTrace);
                }
            }

            // 2. Затем рисуем все узлы
            foreach (var node in diagram.Nodes)
            {
                bool isSelected = selectedNodeIds.Contains(node.Id);
                DrawNode(g, node, isSelected);
            }
        }
        private void DrawGrid(Graphics g, float zoom, PointF panOffset, int gridStep)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(235, 235, 235), 1f)) // Очень светлый серый
            {
                float steppedGrid = gridStep * zoom;

                // Вычисляем начальные точки, чтобы сетка была бесконечной при панорамировании
                float startX = panOffset.X % steppedGrid;
                float startY = panOffset.Y % steppedGrid;

                // Вертикальные линии
                for (float x = startX; x < g.ClipBounds.Width + steppedGrid; x += steppedGrid)
                {
                    g.DrawLine(gridPen, x, 0, x, g.VisibleClipBounds.Height);
                }

                // Горизонтальные линии
                for (float y = startY; y < g.ClipBounds.Height + steppedGrid; y += steppedGrid)
                {
                    g.DrawLine(gridPen, 0, y, g.VisibleClipBounds.Width, y);
                }
            }
        }

        public void DrawNode(Graphics g, Node node, bool isSelected)
        {
            // 1. Считаем реальный размер под текст
            var rect = GetNodeBounds(g, node);

            // 2. Выбираем цвет (используем твой Enum)
            Brush bgBrush = Brushes.LightGray;
            bool rounded = false;

            switch (node.ColorName)
            {
                case NodeColor.Green: bgBrush = Brushes.LightGreen; break;
                case NodeColor.Yellow: bgBrush = Brushes.LightYellow; break;
                case NodeColor.Red:
                    bgBrush = Brushes.LightCoral;
                    rounded = true; // Красные - круглые 
                    break;
            }

            // 3. Рисуем форму
            var penColor = isSelected ? Color.DodgerBlue : Color.Black;
            float penWidth = isSelected ? 3f : 1f;

            using (var pen = new Pen(penColor, penWidth))
            {
                if (rounded)
                {
                    using (var path = GetRoundedRect(rect, 15))
                    {
                        g.FillPath(bgBrush, path);
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    g.FillRectangle(bgBrush, rect);
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }

                var stringFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(node.Title, _mainFont, Brushes.Black, rect, stringFormat);
            }

        }

        // Вспомогательный метод для скругленных углов
        private GraphicsPath GetRoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        //СТРЕЛКИ
        public void DrawConnection(Graphics g, Node from, Node to, Edge edge, bool isSelected/* = false*/, float zoom, TraceResult activeTrace)
        {
            if (from == null || to == null) return;

            // 1. Вычисляем границы обоих узлов
            var fromRect = GetNodeBounds(g, from);
            var toRect = GetNodeBounds(g, to);

            // 2. Находим точки на границах, смотрящие друг на друга
            var startPoint = GetRectBoundaryPointTowards(fromRect, new PointF(to.X, to.Y));
            var endPoint = GetRectBoundaryPointTowards(toRect, new PointF(from.X, from.Y));
            
            //для мини-карты
            bool isMinimap = g.Transform.Elements[0] < 0.2f;

            // Проверяем, участвует ли эта связь в трассировке
            
            bool isHighlighted = activeTrace != null && edge != null && activeTrace.PathEdgeIds.Contains(edge.Id);

            //Color edgeColor = isSelected ? Color.DodgerBlue : Color.Black; // Красный как у узлов
            //float edgeThickness = isSelected ? 3f : 2f; // Чуть толще при выделении
            Color edgeColor = Color.Black;
            float edgeThickness = 2f;
            if (isHighlighted)
            {
                edgeColor = Color.OrangeRed; // Цвет трассировки
                edgeThickness = 4f;          // Делаем толще
            }
            else if (isSelected)
            {
                edgeColor = Color.DodgerBlue; //выделение
                edgeThickness = 3f;
            }

            float baseThickness = isSelected ? 4f : 2f;
            float currentPenWidth = baseThickness / zoom;


            float worldArrowW = 8f;
            float worldArrowH = 20f;

            float finalCapW = worldArrowW / currentPenWidth;
            float finalCapH = worldArrowH / currentPenWidth;


            // 3. Рисуем стрелку между этими точками
            using (var pen = new Pen(edgeColor, edgeThickness))
            {
                if (!isMinimap)
                {
                    float capW = finalCapW;
                    float capH = finalCapH;

                    pen.CustomEndCap = new AdjustableArrowCap(capW, capH, true);
                }
                else 
                {
                    pen.Width = 1f;
                }

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(pen, startPoint, endPoint);
            }
        }
    }
}