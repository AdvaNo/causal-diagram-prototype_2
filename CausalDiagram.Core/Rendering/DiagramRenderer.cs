using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using CausalDiagram.Core.Models;

namespace CausalDiagram.Rendering
{
    public class DiagramRenderer
    {
        // Настройки шрифтов
        private readonly Font _mainFont = new Font("Segoe UI", 10f);

        // Исправление бага: Динамический расчет размера узла
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

        public void Render(Graphics g, Diagram diagram, HashSet<Guid> selectedNodeIds, Edge selectedEdge, float zoom)
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
                    DrawConnection(g, fromNode, toNode, isEdgeSelected,zoom);
                }
            }

            // 2. Затем рисуем все узлы
            foreach (var node in diagram.Nodes)
            {
                bool isSelected = selectedNodeIds.Contains(node.Id);
                DrawNode(g, node, isSelected);
            }
        }

        public void DrawNode(Graphics g, Node node, bool isSelected)
        {
            // 1. Считаем реальный размер под текст
            //var size = CalculateNodeSize(g, node);

            var rect = GetNodeBounds(g, node);

            // Центрируем прямоугольник относительно координат узла
            //var rect = new RectangleF(
            //    node.X - size.Width / 2f,
            //    node.Y - size.Height / 2f,
            //    size.Width,
            //    size.Height);

            // 2. Выбираем цвет (используем твой Enum)
            Brush bgBrush = Brushes.LightGray;
            bool rounded = false;

            switch (node.ColorName)
            {
                case NodeColor.Green: bgBrush = Brushes.LightGreen; break;
                case NodeColor.Yellow: bgBrush = Brushes.LightYellow; break;
                case NodeColor.Red:
                    bgBrush = Brushes.LightCoral;
                    rounded = true; // Красные - круглые (по логике прототипа)
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

        //    // 4. Рисуем текст (уже гарантированно влезает)
        //    var stringFormat = new StringFormat
        //    {
        //        Alignment = StringAlignment.Center,
        //        LineAlignment = StringAlignment.Center
        //    };
        //    g.DrawString(node.Title, _mainFont, Brushes.Black, rect, stringFormat);
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
        public void DrawConnection(Graphics g, Node from, Node to, bool isSelected/* = false*/, float zoom)
        {
            if (from == null || to == null) return;

            // 1. Вычисляем границы обоих узлов
            var fromRect = GetNodeBounds(g, from);
            var toRect = GetNodeBounds(g, to);

            // 2. Находим точки на границах, смотрящие друг на друга
            var startPoint = GetRectBoundaryPointTowards(fromRect, new PointF(to.X, to.Y));
            var endPoint = GetRectBoundaryPointTowards(toRect, new PointF(from.X, from.Y));

            //float baseWidth = isSelected ? 4f : 2f;
            //float adjustedWidth = baseWidth / zoom;

            float baseThickness = isSelected ? 4f : 2f;
            float currentPenWidth = baseThickness / zoom;


            float worldArrowW = 8f;
            float worldArrowH = 20f;

            float finalCapW = worldArrowW / currentPenWidth;
            float finalCapH = worldArrowH / currentPenWidth;

            // ВЫБОР ЦВЕТА: если выделено — рисуем синим и толстым, если нет — серым
            Color penColor = isSelected ? Color.DodgerBlue : Color.Gray;


            // 3. Рисуем стрелку между этими точками
            using (var pen = new Pen(Color.Gray, 2f))
            {
                if (finalCapW > 0.01f && finalCapH > 0.01f)
                {
                    pen.CustomEndCap = new AdjustableArrowCap(finalCapW, finalCapH, true);
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(pen, startPoint, endPoint);
            }

            
            //float penWidth = isSelected ? 4f : 2f;

            //using (var pen = new Pen(penColor, penWidth))
            //{
            //    pen.CustomEndCap = new AdjustableArrowCap(6, 12, true);
            //    g.DrawLine(pen, startPoint, endPoint);
            //}

        }
    }
}