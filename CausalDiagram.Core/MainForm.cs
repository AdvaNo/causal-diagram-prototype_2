using System;
using System.Drawing;
using System.Windows.Forms;
using CausalDiagram.Core.Models;
using CausalDiagram.Services;
using CausalDiagram.Rendering;
using CausalDiagram.Controllers;
using System.Linq;

namespace CausalDiagram.Core
{
    public partial class MainForm : Form
    {
        
        // Состояние приложения
        private Diagram _diagram = new Diagram();
        private Panel canvas;

        // Наши новые сервисы (внедрение зависимостей "на минималках")
        private readonly ProjectService _projectService = new ProjectService();
        private readonly DiagramRenderer _renderer = new DiagramRenderer();
        private InteractionController _interaction;

        // Параметры отображения (Camera)
        private float _scale = 1.0f;
        private PointF _panOffset = new PointF(0, 0);

        public MainForm()
        {
            InitializeComponent();

            //для теста
            _diagram.Nodes.Add(new Node { Title = "Короткий", X = 100, Y = 100, ColorName = NodeColor.Green });
            _diagram.Nodes.Add(new Node { Title = "Очень длинный текст причины, который раньше не влезал", X = 300, Y = 100, ColorName = NodeColor.Red });

            // Настройка окна
            this.Text = "Диаграмма Злотина-Зусман";
            this.DoubleBuffered = true; // Важно для отсутствия мерцания!
            this.WindowState = FormWindowState.Maximized;

            _interaction = new InteractionController(_diagram, _renderer);

            // 1. Создаем панель для кнопок
            ToolStrip toolStrip = new ToolStrip();

            // 2. Кнопка "Выбрать" (курсор)
            var btnSelect = new ToolStripButton("Выбрать") { CheckOnClick = true, Checked = true };
            btnSelect.Click += (s, e) => _interaction.CurrentMode = EditorMode.Select;

            // 3. Кнопка "Добавить узел"
            var btnAdd = new ToolStripButton("Новый узел") { CheckOnClick = true };
            btnAdd.Click += (s, e) => _interaction.CurrentMode = EditorMode.AddNode;

            // 4. Логика взаимоисключения кнопок (чтобы нельзя было выбрать обе сразу)
            btnSelect.CheckedChanged += (s, e) => { if (btnSelect.Checked) btnAdd.Checked = false; };
            btnAdd.CheckedChanged += (s, e) => { if (btnAdd.Checked) btnSelect.Checked = false; };

            // 5. Кнопки Сохранить/Загрузить
            var btnSave = new ToolStripButton("Сохранить");
            btnSave.Click += (s, e) => SaveProject();

            var btnLoad = new ToolStripButton("Загрузить");
            btnLoad.Click += (s, e) => LoadProject();

            // Добавляем всё на панель
            toolStrip.Items.AddRange(new ToolStripItem[] { btnSelect, btnAdd, new ToolStripSeparator(), btnSave, btnLoad });

            // Добавляем панель в форму (она сама прилипнет к верху)
            this.Controls.Add(toolStrip);

            // 1. Создаем кнопку
            var btnConnect = new ToolStripButton("Провести связь") { CheckOnClick = true };
            btnConnect.Click += (s, e) => _interaction.CurrentMode = EditorMode.Connect;

            // 2. Добавляем её в логику взаимоисключения (чтобы только одна кнопка была нажата)
            btnConnect.CheckedChanged += (s, e) => {
                if (btnConnect.Checked) { btnSelect.Checked = false; btnAdd.Checked = false; }
            };
            // Не забудь добавить проверку для других кнопок, чтобы они выключали btnConnect!
            btnSelect.CheckedChanged += (s, e) => { if (btnSelect.Checked) { btnAdd.Checked = false; btnConnect.Checked = false; } };
            btnAdd.CheckedChanged += (s, e) => { if (btnAdd.Checked) { btnSelect.Checked = false; btnConnect.Checked = false; } };

            // 3. Добавь кнопку на панель
            toolStrip.Items.Insert(2, btnConnect); // Вставляем после кнопки "Новый узел"

            var btnDelete = new ToolStripButton("Удалить");
            btnDelete.Click += (s, e) => DeleteSelectedItems();

            // Создаем Canvas программно, чтобы не зависеть от дизайнера пока
            canvas = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Включаем DoubleBuffering для панели через Reflection (старый трюк WinForms)
            typeof(Panel).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, canvas, new object[] { true });

            canvas.Paint += Canvas_Paint;
            this.Controls.Add(canvas);

            // Подписываемся на мышь
            canvas.MouseDown += (s, e) => {
                var p = ScreenToCanvas(e.Location);
                Node nodeAtMouse = null;

                bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;

                using (var g = canvas.CreateGraphics())
                {
                    nodeAtMouse = _interaction.FindNodeAt(p, g);
                }

                // ЛОГИКА ВЫБОРА И ПЕРЕМЕЩЕНИЯ
                if (_interaction.CurrentMode == EditorMode.Select)
                {
                    _interaction.HandleMouseDown(p, nodeAtMouse, isCtrl);
                }
                // ЛОГИКА ДОБАВЛЕНИЯ
                else if (_interaction.CurrentMode == EditorMode.AddNode)
                {
                    if (nodeAtMouse == null) // Кликнули по пустому месту
                    {
                        var newNode = new Node
                        {
                            Title = "Новый фактор",
                            X = p.X,
                            Y = p.Y,
                            ColorName = NodeColor.Yellow
                        };
                        _diagram.Nodes.Add(newNode);
                    }
                }
                else if (_interaction.CurrentMode == EditorMode.Connect)
                {
                    if (nodeAtMouse != null)
                    {
                        _interaction.StartConnection(nodeAtMouse);
                    }
                }

                canvas.Invalidate();
            };
            canvas.MouseMove += (s, e) => {
                var p = ScreenToCanvas(e.Location);

                if (_interaction.CurrentMode == EditorMode.Connect && _interaction.StartNodeForConnection != null)
                {
                    _interaction.UpdateTempConnection(p);
                    canvas.Invalidate();
                }
                else
                {
                    _interaction.HandleMouseMove(p, canvas.Size);

                    // Теперь проверяем PrimaryDraggedNode
                    if (_interaction._primaryDraggedNode != null)
                    {
                        canvas.Invalidate();
                    }
                }
            };

            canvas.MouseUp += (s, e) => {
                if (_interaction.CurrentMode == EditorMode.Connect && _interaction.StartNodeForConnection != null)
                {
                    var p = ScreenToCanvas(e.Location);
                    Node endNode;
                    using (var g = canvas.CreateGraphics()) { endNode = _interaction.FindNodeAt(p, g); }

                    // Если отпустили над другим узлом и это не тот же самый узел
                    if (endNode != null && endNode != _interaction.StartNodeForConnection)
                    {
                        // Создаем новую связь (Edge)
                        _diagram.Edges.Add(new Edge
                        {
                            From = _interaction.StartNodeForConnection.Id,
                            To = endNode.Id
                        });
                    }
                    _interaction.EndConnection();
                    canvas.Invalidate();
                }
                _interaction.HandleMouseUp();
            };

            canvas.Paint += Canvas_Paint;
            this.Controls.Add(canvas);

        }



        // Вспомогательный метод для перевода координат экрана в координаты диаграммы
        private PointF ScreenToCanvas(Point p)
        {
            return new PointF((p.X - _panOffset.X) / _scale, (p.Y - _panOffset.Y) / _scale);
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TranslateTransform(_panOffset.X, _panOffset.Y);
            g.ScaleTransform(_scale, _scale);

            // 1. Рисуем связи (стрелки)
            foreach (var edge in _diagram.Edges)
            {
                // Находим узлы по ID (предполагаем, что они есть в диаграмме)
                var fromNode = _diagram.Nodes.FirstOrDefault(n => n.Id == edge.From);
                var toNode = _diagram.Nodes.FirstOrDefault(n => n.Id == edge.To);
                _renderer.DrawConnection(g, fromNode, toNode);
            }
            // Рисуем временную линию связи
            if (_interaction.CurrentMode == EditorMode.Connect && _interaction.StartNodeForConnection != null)
            {
                using (var pen = new Pen(Color.LightBlue, 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawLine(pen,
                        _interaction.StartNodeForConnection.X,
                        _interaction.StartNodeForConnection.Y,
                        _interaction.CurrentTempPoint.X,
                        _interaction.CurrentTempPoint.Y);
                }
            }


            // 2. Рисуем узлы
            foreach (var node in _diagram.Nodes)
            {
                // Проверяем, есть ли ID узла в списке выделенных
                bool isSelected = _interaction.SelectedNodeIds.Contains(node.Id);
                _renderer.DrawNode(g, node, isSelected);
            }
        }

        private void SaveProject()
        {
            using (var sfd = new SaveFileDialog { Filter = "Diagram Files|*.json" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _projectService.Save(_diagram, sfd.FileName);
                    MessageBox.Show("Проект сохранен!");
                }
            }
        }

        private void LoadProject()
        {
            using (var ofd = new OpenFileDialog { Filter = "Diagram Files|*.json" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _diagram = _projectService.Load(ofd.FileName);
                    // Пересоздаем контроллер с новыми данными
                    _interaction = new InteractionController(_diagram, _renderer);
                    this.Invalidate(true); // Полная перерисовка
                    MessageBox.Show("Проект загружен!");
                }
            }
        }

        private void DeleteSelectedItems()
        {
            // Пока удаляем без Undo/Redo, напрямую из модели
            var nodesToDelete = _interaction.SelectedNodeIds.ToList();

            if (nodesToDelete.Count == 0) return;

            if (MessageBox.Show($"Удалить {nodesToDelete.Count} узла(ов)?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                foreach (var id in nodesToDelete)
                {
                    var node = _diagram.Nodes.FirstOrDefault(n => n.Id == id);
                    if (node != null)
                    {
                        // 1. Сначала удаляем все связи, ведущие к этому узлу или от него
                        _diagram.Edges.RemoveAll(edge => edge.From == node.Id || edge.To == node.Id);

                        // 2. Удаляем сам узел
                        _diagram.Nodes.Remove(node);
                    }
                }

                // Очищаем выделение после удаления
                _interaction.SelectedNodeIds.Clear();
                canvas.Invalidate();
            }
        }
    }
}


