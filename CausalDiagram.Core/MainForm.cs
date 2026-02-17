using System;
using System.Drawing;
using System.Windows.Forms;
using CausalDiagram.Core.Models;
using CausalDiagram.Services;
using CausalDiagram.Rendering;
using CausalDiagram.Controllers;
using System.Linq;
using CausalDiagram.Core.Commands;
using System.Collections.Generic;
using CausalDiagram.Commands;

namespace CausalDiagram.Core
{
    public partial class MainForm : Form
    {
        
        // Состояние приложения
        private Diagram _diagram = new Diagram();
        private Panel canvas;
        private PropertyGrid _propertyGrid;

        // Наши новые сервисы (внедрение зависимостей "на минималках")
        private readonly ProjectService _projectService = new ProjectService();
        private readonly DiagramRenderer _renderer = new DiagramRenderer();
        private InteractionController _interaction;
        private readonly CommandManager _commandManager = new CommandManager();

        // Параметры отображения (Camera)
        private float _scale = 1.0f;
        private PointF _panOffset = new PointF(0, 0);

        //для копи+паста
        private List<Node> _clipboard = new List<Node>();
        //для отмены+возврата
        private Dictionary<Guid, (float x, float y)> _dragStartPositions = new Dictionary<Guid, (float x, float y)>();

        public MainForm()
        {
            InitializeComponent();

            //для теста
            //_diagram.Nodes.Add(new Node { Title = "Короткий", X = 100, Y = 100, ColorName = NodeColor.Green });
            //_diagram.Nodes.Add(new Node { Title = "Очень длинный текст причины, который раньше не влезал", X = 300, Y = 100, ColorName = NodeColor.Red });
                        

            // Настройка окна
            this.Text = "Диаграмма Злотина-Зусман";
            this.DoubleBuffered = true; // Важно для отсутствия мерцания!
            this.WindowState = FormWindowState.Maximized;
            this.KeyPreview = true; //чтобы видеть горячие клавиши


            _interaction = new InteractionController(_diagram, _renderer);

            InitPropertyGrid();

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

            var btnDelete = new ToolStripButton("Удалить") { CheckOnClick = true };
            btnDelete.Click += (s, e) => DeleteSelectedItems();

            btnDelete.CheckedChanged += (s, e) => {
                if (btnConnect.Checked) { btnSelect.Checked = false; btnAdd.Checked = false; }
            };

            toolStrip.Items.Insert(3, btnDelete);

            var btnUndo = new ToolStripButton("Отменить") { CheckOnClick = true };
            btnUndo.Click += (s, e) => { _commandManager.Undo(); canvas.Invalidate(); };
            btnUndo.CheckedChanged += (s, e) => {
                if (btnConnect.Checked) { btnSelect.Checked = false; btnAdd.Checked = false; }
            };
            toolStrip.Items.Insert(4, btnUndo);

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

                //bool isCtrl = ModifierKeys.HasFlag(Keys.Control);

                Node nodeAtMouse = null;

                bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;

                using (var g = canvas.CreateGraphics())
                {
                    nodeAtMouse = _interaction.FindNodeAt(p, g);
                }

                // Перед началом движения запоминаем позиции всех выделенных узлов
                _dragStartPositions = _diagram.Nodes
                    .Where(n => _interaction.SelectedNodeIds.Contains(n.Id))
                    .ToDictionary(n => n.Id, n => (n.X, n.Y));
                // ЛОГИКА ВЫБОРА И ПЕРЕМЕЩЕНИЯ
                if (_interaction.CurrentMode == EditorMode.Select)
                {
                    // 1. Сначала отрабатываем выделение
                    _interaction.HandleMouseDown(p, nodeAtMouse, isCtrl);

                    // 2. СРАЗУ ПОСЛЕ выделения очищаем старые данные и записываем новые
                    _dragStartPositions.Clear();
                    foreach (var id in _interaction.SelectedNodeIds)
                    {
                        var node = _diagram.Nodes.FirstOrDefault(n => n.Id == id);
                        if (node != null)
                        {
                            _dragStartPositions[node.Id] = (node.X, node.Y);
                        }
                    }

                    if (nodeAtMouse != null) _propertyGrid.SelectedObject = nodeAtMouse;


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
                        //_diagram.Nodes.Add(newNode);
                        var command = new CausalDiagram.Commands.AddNodeCommand(_diagram, newNode);
                        _commandManager.Execute(command);
                    }
                }
                else if (_interaction.CurrentMode == EditorMode.Connect)
                {
                    if (nodeAtMouse != null)
                    {
                        _interaction.StartConnection(nodeAtMouse);
                    }
                }

                if (nodeAtMouse != null)
                {
                    _propertyGrid.SelectedObject = nodeAtMouse; // Показываем свойства узла
                    _propertyGrid.Visible = true; // Показываем, если выбрали узел
                }
                else
                {
                    _propertyGrid.SelectedObject = null;
                    _propertyGrid.Visible = false;// Скрываем, если кликнули по пустому месту
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
                    _interaction.HandleMouseMove(p, canvas.ClientSize);

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
                        var startId = _interaction.StartNodeForConnection.Id;
                        var endId = endNode.Id;

                        // ПРОВЕРКА: есть ли уже связь в ЛЮБОМ направлении?
                        bool exists = _diagram.Edges.Any(edge =>
                            (edge.From == startId && edge.To == endId) ||
                            (edge.From == endId && edge.To == startId));

                        if (!exists)
                        {
                            var newEdge = new Edge { From = startId, To = endId };

                            // Используем команду для отмены!
                            var command = new CausalDiagram.Commands.AddEdgeCommand(_diagram, newEdge);
                            _commandManager.Execute(command);
                        }
                        else
                        {
                            // Можно добавить звук ошибки или статус в статус-бар
                            MessageBox.Show("Связь между этими узлами уже существует!");
                        }
                        //// Создаем новую связь (Edge)
                        //_diagram.Edges.Add(new Edge
                        //{
                        //    From = _interaction.StartNodeForConnection.Id,
                        //    To = endNode.Id
                        //});
                    }
                    _interaction.EndConnection();
                    canvas.Invalidate();
                }
                _interaction.HandleMouseUp();
                if (_dragStartPositions.Count > 0)
                {
                    var moves = new List<(Node node, float oldX, float oldY, float newX, float newY)>();

                    foreach (var entry in _dragStartPositions)
                    {
                        var node = _diagram.Nodes.FirstOrDefault(n => n.Id == entry.Key);
                        if (node != null)
                        {
                            // Проверяем, изменились ли координаты (с допуском, чтобы не ловить микро-сдвиги)
                            if (Math.Abs(node.X - entry.Value.x) > 0.1f || Math.Abs(node.Y - entry.Value.y) > 0.1f)
                            {
                                moves.Add((node, entry.Value.x, entry.Value.y, node.X, node.Y));
                            }
                        }
                    }

                    if (moves.Count > 0)
                    {
                        // Создаем команду. Важно: мы не вызываем Execute(), 
                        // так как узлы уже передвинуты мышкой, просто кладем в стек.
                        var moveCommand = new CausalDiagram.Commands.MoveNodeCommand(moves);
                        _commandManager.PushToUndoStack(moveCommand);
                    }
                    _dragStartPositions.Clear();
                }
            };

            canvas.Paint += Canvas_Paint;
            //this.Controls.Add(canvas);

        }



        // Вспомогательный метод для перевода координат экрана в координаты диаграммы
        private PointF ScreenToCanvas(Point p)
        {
            return new PointF((p.X - _panOffset.X) / _scale, (p.Y - _panOffset.Y) / _scale);
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            RenderDiagram(e.Graphics);
            //var g = e.Graphics;
            //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            //g.TranslateTransform(_panOffset.X, _panOffset.Y);
            //g.ScaleTransform(_scale, _scale);

            //// 1. Рисуем связи (стрелки)
            //foreach (var edge in _diagram.Edges)
            //{
            //    // Находим узлы по ID (предполагаем, что они есть в диаграмме)
            //    var fromNode = _diagram.Nodes.FirstOrDefault(n => n.Id == edge.From);
            //    var toNode = _diagram.Nodes.FirstOrDefault(n => n.Id == edge.To);
            //    _renderer.DrawConnection(g, fromNode, toNode);
            //}
            //// Рисуем временную линию связи
            //if (_interaction.CurrentMode == EditorMode.Connect && _interaction.StartNodeForConnection != null)
            //{
            //    using (var pen = new Pen(Color.LightBlue, 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            //    {
            //        g.DrawLine(pen,
            //            _interaction.StartNodeForConnection.X,
            //            _interaction.StartNodeForConnection.Y,
            //            _interaction.CurrentTempPoint.X,
            //            _interaction.CurrentTempPoint.Y);
            //    }
            //}


            //// 2. Рисуем узлы
            //foreach (var node in _diagram.Nodes)
            //{
            //    // Проверяем, есть ли ID узла в списке выделенных
            //    bool isSelected = _interaction.SelectedNodeIds.Contains(node.Id);
            //    _renderer.DrawNode(g, node, isSelected);
            //}
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
            // 1. Получаем список ID выделенных узлов
            var nodesToDeleteIds = _interaction.SelectedNodeIds.ToList();

            if (nodesToDeleteIds.Count == 0) return;

            // 2. Спрашиваем пользователя
            var result = MessageBox.Show(
                $"Удалить {nodesToDeleteIds.Count} узла(ов) и связанные с ними линии?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // 3. Создаем команду, передавая ей текущую диаграмму и список ID
                var command = new DeleteNodeCommand(_diagram, nodesToDeleteIds);

                // 4. Отправляем команду в CommandManager. 
                // Он сам вызовет метод Execute() и сохранит команду в стек Undo.
                _commandManager.Execute(command);

                // 5. Очищаем выделение и перерисовываем
                _interaction.SelectedNodeIds.Clear();
                canvas.Invalidate();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Обработка Ctrl + Z (Undo)
            if (keyData == (Keys.Control | Keys.Z))
            {
                _commandManager.Undo();
                canvas.Invalidate(); // Перерисовываем холст, чтобы увидеть изменения
                return true; // Говорим системе, что мы сами обработали нажатие
            }

            // Обработка Ctrl + Y (Redo)
            if (keyData == (Keys.Control | Keys.Y))
            {
                _commandManager.Redo();
                canvas.Invalidate();
                return true;
            }

            // Обработка Delete
            if (keyData == Keys.Delete)
            {
                DeleteSelectedItems();
                return true;
            }

            // --- Копирование (Ctrl + C) ---
            if (keyData == (Keys.Control | Keys.C))
            {
                _clipboard.Clear();
                var selectedNodes = _diagram.Nodes.Where(n => _interaction.SelectedNodeIds.Contains(n.Id)).ToList();

                foreach (var node in selectedNodes)
                {
                    // Создаем "клон" объекта, чтобы оригинал и копия не были связаны
                    _clipboard.Add(new Node
                    {
                        Title = node.Title,
                        Description = node.Description,
                        ColorName = node.ColorName,
                        X = node.X,
                        Y = node.Y
                    });
                }
                return true;
            }

            // --- Вставка (Ctrl + V) ---
            if (keyData == (Keys.Control | Keys.V))
            {
                if (_clipboard.Count > 0)
                {
                    var nodesToPaste = new List<Node>();
                    _interaction.SelectedNodeIds.Clear();

                    foreach (var item in _clipboard)
                    {
                        var newNode = new Node
                        {
                            Id = Guid.NewGuid(),
                            Title = item.Title + " (копия)",
                            Description = item.Description,
                            ColorName = item.ColorName,
                            X = item.X + 20,
                            Y = item.Y + 20
                        };
                        nodesToPaste.Add(newNode);
                        _interaction.SelectedNodeIds.Add(newNode.Id);
                    }

                    // Создаем и выполняем команду вставки
                    var command = new PasteCommand(_diagram, nodesToPaste);
                    _commandManager.Execute(command);

                    canvas.Invalidate();
                }
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        private void RenderDiagram(Graphics g)
        {
            //var e = g.Graphics;
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

        private void InitPropertyGrid()
        {
            _propertyGrid = new PropertyGrid();
            _propertyGrid.Dock = DockStyle.Right;
            _propertyGrid.Width = 250;
            _propertyGrid.Parent = this;

            // --- НАСТРОЙКИ ВНЕШНЕГО ВИДА ---
            _propertyGrid.ToolbarVisible = false; // Убираем кнопки сверху
            _propertyGrid.HelpVisible = true;     // Оставляем нижнюю панель с подсказками (можно false, если не нужна)
            _propertyGrid.PropertySort = PropertySort.Categorized; // Или Alphabetical
                                                                   // -------------------------------

            _propertyGrid.PropertyValueChanged += (s, e) => canvas.Invalidate();
            this.Controls.Add(_propertyGrid);
        }

        private void CopySelected()
        {
            _clipboard.Clear();
            var selectedNodes = _diagram.Nodes.Where(n => _interaction.SelectedNodeIds.Contains(n.Id));
            foreach (var node in selectedNodes)
            {
                // Создаем глубокую копию, чтобы не менять оригиналы
                _clipboard.Add(new Node
                {
                    Title = node.Title + " (копия)",
                    X = node.X,
                    Y = node.Y,
                    ColorName = node.ColorName
                });
            }
        }

        private void Paste()
        {
            if (_clipboard.Count == 0) return;

            _interaction.SelectedNodeIds.Clear();
            foreach (var clone in _clipboard)
            {
                var newNode = new Node
                {
                    Id = Guid.NewGuid(), // Новый ID обязателен!
                    Title = clone.Title,
                    X = clone.X + 20, // Сдвиг, чтобы не слиплись
                    Y = clone.Y + 20,
                    ColorName = clone.ColorName
                };
                _diagram.Nodes.Add(newNode);
                _interaction.SelectedNodeIds.Add(newNode.Id);
            }
            canvas.Invalidate();
        }
    }
}


