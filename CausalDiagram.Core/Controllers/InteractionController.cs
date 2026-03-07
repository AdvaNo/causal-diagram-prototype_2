using System;
using System.Drawing;
using System.Linq;
using CausalDiagram.Core.Models;
using CausalDiagram.Rendering;
using System.Collections.Generic;

namespace CausalDiagram.Controllers
{
    public enum EditorMode { Select, AddNode, Connect }

    public class InteractionController
    {
        private readonly Diagram _diagram;
        private readonly DiagramRenderer _renderer;

        public EditorMode CurrentMode { get; set; } = EditorMode.Select;

        // Состояние для Drag-and-Drop
        //public Node DraggedNode { get; private set; }
        public HashSet<Guid> SelectedNodeIds { get; private set; } = new HashSet<Guid>();
        public Node _primaryDraggedNode; // Тот узел, за который непосредственно тянем
        private PointF _dragOffset;

        public InteractionController(Diagram diagram, DiagramRenderer renderer)
        {
            _diagram = diagram;
            _renderer = renderer;
        }

        // Метод поиска узла под мышкой (HitTest)
        public Node FindNodeAt(PointF mousePos, Graphics g)
        {
            // Идем с конца списка, чтобы выбирать верхние узлы
            foreach (var node in _diagram.Nodes.AsEnumerable().Reverse())
            {
                var size = _renderer.CalculateNodeSize(g, node);
                var rect = new RectangleF(
                    node.X - size.Width / 2f,
                    node.Y - size.Height / 2f,
                    size.Width,
                    size.Height);

                if (rect.Contains(mousePos)) return node;
            }
            return null;
        }

        public void HandleMouseDown(PointF canvasPos, Node nodeUnderMouse, bool isCtrlPressed)
        {
            if (CurrentMode == EditorMode.Select)
            {
                if (nodeUnderMouse != null)
                {
                    // Логика выделения с Ctrl
                    if (isCtrlPressed)
                    {
                        // Если зажат Ctrl - переключаем выделение (Toggle)
                        if (SelectedNodeIds.Contains(nodeUnderMouse.Id))
                            SelectedNodeIds.Remove(nodeUnderMouse.Id);
                        else
                            SelectedNodeIds.Add(nodeUnderMouse.Id);
                    }
                    else
                    {
                        // Если Ctrl не зажат и кликнули на невыделенный узел - сбрасываем остальных
                        if (!SelectedNodeIds.Contains(nodeUnderMouse.Id))
                        {
                            SelectedNodeIds.Clear();
                            SelectedNodeIds.Add(nodeUnderMouse.Id);
                        }
                        // Если кликнули на уже выделенный - ничего не сбрасываем, готовимся тащить группу
                    }

                    // Готовимся к перетаскиванию
                    if (SelectedNodeIds.Contains(nodeUnderMouse.Id))
                    {
                        _primaryDraggedNode = nodeUnderMouse;
                        _dragOffset = new PointF(canvasPos.X - nodeUnderMouse.X, canvasPos.Y - nodeUnderMouse.Y);
                    }
                }
                else
                {
                    // Клик в пустое место без Ctrl - сброс выделения
                    if (!isCtrlPressed) SelectedNodeIds.Clear();
                }
            }
        }

        public void HandleMouseMove(PointF canvasPos, Size boundary)
        {
            if (_primaryDraggedNode != null)
            {
                // 1. Вычисляем, куда МЫШКА хочет передвинуть главный узел
                float targetX = canvasPos.X - _dragOffset.X;
                float targetY = canvasPos.Y - _dragOffset.Y;

                // 2. Вычисляем дельту (желаемый сдвиг)
                float deltaX = targetX - _primaryDraggedNode.X;
                float deltaY = targetY - _primaryDraggedNode.Y;

                // Если сдвига нет, ничего не делаем
                if (deltaX == 0 && deltaY == 0) return;

                var selectedNodes = SelectedNodeIds
                    .Select(id => _diagram.Nodes.FirstOrDefault(n => n.Id == id))
                    .Where(n => n != null)
                    .ToList();

                    foreach (var node in selectedNodes)
                    {
                        node.X += deltaX;
                        node.Y += deltaY;
                    }
                
            }
        }

        public void HandleMouseUp()
        {
            _primaryDraggedNode = null;
        }


        public Node StartNodeForConnection { get; private set; }
        public PointF CurrentTempPoint { get; private set; }

        public void StartConnection(Node startNode)
        {
            StartNodeForConnection = startNode;
            CurrentTempPoint = new PointF(startNode.X, startNode.Y);
        }

        public void UpdateTempConnection(PointF currentPos)
        {
            CurrentTempPoint = currentPos;
        }

        public void EndConnection()
        {
            StartNodeForConnection = null;
        }
    }
}