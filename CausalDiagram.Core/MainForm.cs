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
using System.Drawing.Drawing2D;


namespace CausalDiagram.Core
{
    public partial class MainForm : Form
    {
        
        // Состояние приложения
        private Diagram _diagram = new Diagram();
        private Panel canvas;
        private PropertyGrid _propertyGrid;
        private Edge _selectedEdge;
        private ClipboardData _diagramClipboard;

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

        // Цвет по умолчанию
        private NodeColor _currentSelectedColor = NodeColor.Yellow;
        private ToolStripComboBox colorSelector;

        //zoom+pan
        private float _zoom = 1.0f;
        private Point _lastMousePos; // Для отслеживания перемещения мыши при панорамировании
        private bool _isPanning = false; // Зажат ли "режим перемещения"

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
            AutoScroll = false;
            AutoSize = false;

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


            // 1. Создаем сам выпадающий список
            colorSelector = new ToolStripComboBox();
            colorSelector.DropDownStyle = ComboBoxStyle.DropDownList; // Запрещаем вводить текст руками, только выбор

            // 2. Добавляем три варианта
            colorSelector.Items.Add("Желтый");
            colorSelector.Items.Add("Красный");
            colorSelector.Items.Add("Зеленый");

            // 3. Настраиваем цвет по умолчанию (Желтый)
            colorSelector.SelectedIndex = 0;
            colorSelector.BackColor = Color.LightYellow;
            _currentSelectedColor = NodeColor.Yellow; 

            // 4. Логика при выборе нового цвета
            colorSelector.SelectedIndexChanged += (s, e) =>
            {
                // Меняем и переменную для новых узлов, и фон самого списка
                switch (colorSelector.SelectedItem.ToString())
                {
                    case "Желтый":
                        _currentSelectedColor = NodeColor.Yellow;
                        colorSelector.BackColor = Color.LightYellow;
                        break;
                    case "Красный":
                        _currentSelectedColor = NodeColor.Red;
                        colorSelector.BackColor = Color.LightCoral; // Используем мягкие цвета, чтобы текст хорошо читался
                        break;
                    case "Зеленый":
                        _currentSelectedColor = NodeColor.Green;
                        colorSelector.BackColor = Color.LightGreen;
                        break;
                }

                // Снимаем фокус с выпадающего списка, чтобы снова работали горячие клавиши 
                canvas.Focus();
            };

            // 5. Добавляем текстовую метку и сам список на панель инструментов
            toolStrip.Items.Add(new ToolStripLabel("Цвет нового фактора:"));
            toolStrip.Items.Add(colorSelector);

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

            //canvas.Paint += Canvas_Paint;
            this.Controls.Add(canvas);

            canvas.Paint += (s, e) => {
                //e.Graphics.Clear(Color.White);
                //e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                //Matrix matrix = new Matrix();
                //matrix.Translate(_panOffset.X, _panOffset.Y);
                //matrix.Scale(_zoom, _zoom);
                //e.Graphics.Transform = matrix;

                //// ВЫЗОВ: передаем графику, диаграмму и список ID выделенных узлов
                //_renderer.Render(e.Graphics, _diagram, _interaction.SelectedNodeIds);

                //e.Graphics.ResetTransform();

                using (Bitmap buffer = new Bitmap(canvas.Width, canvas.Height))
                {
                    using (Graphics g = Graphics.FromImage(buffer))
                    {
                        // Настраиваем качество внутри буфера
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.White); // Чистим "виртуальный" лист

                        // 2. Применяем зум и панорамирование
                        Matrix matrix = new Matrix();
                        matrix.Translate(_panOffset.X, _panOffset.Y);
                        matrix.Scale(_zoom, _zoom);
                        g.Transform = matrix;

                        // 3. Рисуем диаграмму В БУФЕР
                        _renderer.Render(g, _diagram, _interaction.SelectedNodeIds, _selectedEdge, _zoom);

                        g.ResetTransform();
                    }

                    // 4. Отрисовываем готовый буфер на реальный экран (без задержек и теней)
                    e.Graphics.DrawImage(buffer, 0, 0);
                }
            };

            // Подписываемся на мышь
            canvas.MouseDown += (s, e) => {
                var p = ScreenToCanvas(e.Location);

                //bool isCtrl = ModifierKeys.HasFlag(Keys.Control);

                _selectedEdge = FindEdgeAt(p); // Вызываем метод поиска

              

                Node nodeAtMouse = null;

                bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;

                if (e.Button == MouseButtons.Right)
                {
                    _isPanning = true;
                    _lastMousePos = e.Location;
                    canvas.Cursor = Cursors.NoMove2D; // Меняем курсор для красоты перемещения
                }

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
                            ColorName = _currentSelectedColor /*NodeColor.Yellow*/
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

                if (_isPanning)
                {
                    // Вычисляем, на сколько сдвинулась мышь
                    float dx = e.X - _lastMousePos.X;
                    float dy = e.Y - _lastMousePos.Y;

                    _panOffset.X += dx;
                    _panOffset.Y += dy;

                    _lastMousePos = e.Location;
                    canvas.Invalidate();
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
                            if (Math.Abs(node.X - entry.Value.x) > 2.0f || Math.Abs(node.Y - entry.Value.y) > 2.0f)
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

                if (e.Button == MouseButtons.Right)
                {
                    _isPanning = false;
                    canvas.Cursor = Cursors.Default;
                }
            };

            canvas.MouseDoubleClick += (s, e) => {
                var p = ScreenToCanvas(e.Location);
                Node node;
                using (var g = canvas.CreateGraphics()) { node = _interaction.FindNodeAt(p, g); }

                if (node != null)
                {
                    ShowEditBox(node);
                }
            };

            canvas.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Создаем матрицу трансформации
                Matrix matrix = new Matrix();
                matrix.Translate(_panOffset.X, _panOffset.Y);
                matrix.Scale(_zoom, _zoom);

                e.Graphics.Transform = matrix; // Применяем матрицу

                // Теперь Renderer просто рисует в своих обычных координатах, 
                // а GDI+ сама их масштабирует и двигает
                _renderer.Render(e.Graphics, _diagram, _interaction.SelectedNodeIds, _selectedEdge, _zoom);

                e.Graphics.ResetTransform(); // Сбрасываем, чтобы UI (если есть) не уплыл
            };

            canvas.MouseWheel += (s, e) => {
                float zoomStep = 1.1f;
                float oldZoom = _zoom;

                if (e.Delta > 0) _zoom *= zoomStep;
                else _zoom /= zoomStep;

                // Ограничим зум, чтобы не уйти в бесконечность
                _zoom = Math.Max(0.1f, Math.Min(_zoom, 5.0f));

                // Магия: корректируем смещение, чтобы курсор оставался над той же точкой холста
                float mouseX = e.X;
                float mouseY = e.Y;

                _panOffset.X = mouseX - (mouseX - _panOffset.X) * (_zoom / oldZoom);
                _panOffset.Y = mouseY - (mouseY - _panOffset.Y) * (_zoom / oldZoom);

                canvas.Invalidate();
            };

            colorSelector.SelectedIndexChanged += (s, e) =>
            {
                NodeColor newColor = NodeColor.Yellow; // значение по умолчанию

                // 1. Твоя существующая логика определения цвета
                switch (colorSelector.SelectedItem.ToString())
                {
                    case "Желтый":
                        newColor = NodeColor.Yellow;
                        colorSelector.BackColor = Color.LightYellow;
                        break;
                    case "Красный":
                        newColor = NodeColor.Red;
                        colorSelector.BackColor = Color.LightCoral;
                        break;
                    case "Зеленый":
                        newColor = NodeColor.Green;
                        colorSelector.BackColor = Color.LightGreen;
                        break;
                }

                // Обновляем глобальную переменную для будущих узлов
                _currentSelectedColor = newColor;

                // 2. НОВАЯ ЛОГИКА: Меняем цвет уже выделенным узлам
                if (_interaction.SelectedNodeIds.Any())
                {
                    var selectedNodes = _diagram.Nodes
                        .Where(n => _interaction.SelectedNodeIds.Contains(n.Id))
                        .ToList();

                    // Создаем команду для Undo/Redo (код команды я давал выше)
                    var command = new ChangeColorCommand(selectedNodes, newColor);
                    _commandManager.Execute(command);

                    canvas.Invalidate(); // Перерисовываем, чтобы увидеть изменения
                }

                // 3. Возвращаем фокус на холст (очень важно для горячих клавиш!)
                canvas.Focus();
            };
            //canvas.Paint += Canvas_Paint;
            //this.Controls.Add(canvas);

        }



        // Вспомогательный метод для перевода координат экрана в координаты диаграммы
        private PointF ScreenToCanvas(Point p)
        {
            //return new PointF((p.X - _panOffset.X) / _scale, (p.Y - _panOffset.Y) / _scale);
            return new PointF(
                (p.X - _panOffset.X) / _zoom,
                (p.Y - _panOffset.Y) / _zoom
            );
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
            //using (var sfd = new SaveFileDialog { Filter = "Diagram Files|*.json" })
            //{
            //    if (sfd.ShowDialog() == DialogResult.OK)
            //    {
            //        _projectService.Save(_diagram, sfd.FileName);
            //        MessageBox.Show("Проект сохранен!");
            //    }
            //}
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Causal Diagram Files (*.json)|*.json";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        DiagramSerializer.SaveToFile(sfd.FileName, _diagram);
                        MessageBox.Show("Диаграмма успешно сохранена!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
                    }
                }
            }
        }

        private void LoadProject()
        {
            //using (var ofd = new OpenFileDialog { Filter = "Diagram Files|*.json" })
            //{
            //    if (ofd.ShowDialog() == DialogResult.OK)
            //    {
            //        _diagram = _projectService.Load(ofd.FileName);
            //        // Пересоздаем контроллер с новыми данными
            //        _interaction = new InteractionController(_diagram, _renderer);
            //        this.Invalidate(true); // Полная перерисовка
            //        MessageBox.Show("Проект загружен!");
            //    }
            //}
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Causal Diagram Files (*.json)|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Загружаем новую диаграмму
                        var loadedDiagram = DiagramSerializer.LoadFromFile(ofd.FileName);

                        // ВАЖНО: Обновляем ссылку в контроллере и форме
                        // (Предполагается, что у тебя есть метод ResetDiagram или аналогичный)
                        _diagram.Nodes.Clear();
                        _diagram.Edges.Clear();
                        _diagram.Nodes.AddRange(loadedDiagram.Nodes);
                        _diagram.Edges.AddRange(loadedDiagram.Edges);

                        _commandManager.Clear(); // Очищаем историю Undo/Redo при загрузке нового файла
                        canvas.Invalidate();
                        MessageBox.Show("Диаграмма загружена!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при загрузке: {ex.Message}");
                    }
                }
            }
        }

        private void DeleteSelectedItems()
        {
            //// 1. Получаем список ID выделенных узлов
            //var nodesToDeleteIds = _interaction.SelectedNodeIds.ToList();

            //if (nodesToDeleteIds.Count == 0) return;

            //// 2. Спрашиваем пользователя
            //var result = MessageBox.Show(
            //    $"Удалить {nodesToDeleteIds.Count} узла(ов) и связанные с ними линии?",
            //    "Подтверждение",
            //    MessageBoxButtons.YesNo,
            //    MessageBoxIcon.Question);

            //if (result == DialogResult.Yes)
            //{
            //    // 3. Создаем команду, передавая ей текущую диаграмму и список ID
            //    var command = new DeleteNodeCommand(_diagram, nodesToDeleteIds);

            //    // 4. Отправляем команду в CommandManager. 
            //    // Он сам вызовет метод Execute() и сохранит команду в стек Undo.
            //    _commandManager.Execute(command);

            //    // 5. Очищаем выделение и перерисовываем
            //    _interaction.SelectedNodeIds.Clear();
            //    canvas.Invalidate();
            //}

            //if (_selectedEdge != null)
            //{
            //    var command = new DeleteEdgeCommand(_diagram, _selectedEdge);
            //    _commandManager.Execute(command);

            //    _selectedEdge = null; // Сбрасываем выделение
            //    canvas.Invalidate();
            //}
            //// 2. Если выделены узлы
            //else if (_interaction.SelectedNodeIds.Count > 0)
            //{
            //    var nodesToDelete = _diagram.Nodes
            //        .Where(n => _interaction.SelectedNodeIds.Contains(n.Id))
            //        .ToList();

            //    // Нам нужна комплексная команда, которая удалит и узлы, и связанные с ними стрелки
            //    var command = new DeleteNodeCommand(_diagram, nodesToDelete);
            //    _commandManager.Execute(command);

            //    _interaction.SelectedNodeIds.Clear();
            //    canvas.Invalidate();
            //}
            // 1. Сначала проверяем, выделена ли отдельная связь
            if (_selectedEdge != null)
            {
                var command = new DeleteEdgeCommand(_diagram, _selectedEdge);
                _commandManager.Execute(command);

                _selectedEdge = null; // Сбрасываем выделение
                canvas.Invalidate();
                return; // Если удалили связь, узлы не трогаем
            }

            // 2. Если связи не выделены, удаляем выделенные узлы
            if (_interaction.SelectedNodeIds.Count > 0)
            {
                // Передаем  список Guid, как просит твой конструктор
                var nodeIdsToDelete = _interaction.SelectedNodeIds.ToList();

                var command = new DeleteNodeCommand(_diagram, nodeIdsToDelete);
                _commandManager.Execute(command);

                _interaction.SelectedNodeIds.Clear();
                canvas.Invalidate();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            //if (colorSelector.Control.Focused)
            //{
            //    return base.ProcessCmdKey(ref msg, keyData);
            //}
            if ((colorSelector != null && colorSelector.Control.Focused) || this.ActiveControl is TextBox)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }
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
                //_clipboard.Clear();
                //var selectedNodes = _diagram.Nodes.Where(n => _interaction.SelectedNodeIds.Contains(n.Id)).ToList();

                //foreach (var node in selectedNodes)
                //{
                //    // Создаем "клон" объекта, чтобы оригинал и копия не были связаны
                //    _clipboard.Add(new Node
                //    {
                //        Title = node.Title,
                //        Description = node.Description,
                //        ColorName = node.ColorName,
                //        X = node.X,
                //        Y = node.Y
                //    });
                //}
                CopySelected();
                return true;
            }

            // --- Вставка (Ctrl + V) ---
            if (keyData == (Keys.Control | Keys.V))
            {
                //if (_clipboard.Count > 0)
                //{
                //    var nodesToPaste = new List<Node>();
                //    _interaction.SelectedNodeIds.Clear();

                //    foreach (var item in _clipboard)
                //    {
                //        var newNode = new Node
                //        {
                //            Id = Guid.NewGuid(),
                //            Title = item.Title + " (копия)",
                //            Description = item.Description,
                //            ColorName = item.ColorName,
                //            X = item.X + 20,
                //            Y = item.Y + 20
                //        };
                //        nodesToPaste.Add(newNode);
                //        _interaction.SelectedNodeIds.Add(newNode.Id);
                //    }

                //    // Создаем и выполняем команду вставки
                //    var command = new PasteCommand(_diagram, nodesToPaste);
                //    _commandManager.Execute(command);

                //    canvas.Invalidate();
                //}
                Paste();
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
                bool isEdgeSelected = (edge == _selectedEdge);
                _renderer.DrawConnection(g, fromNode, toNode, isEdgeSelected, _zoom);
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
            var selectedIds = _interaction.SelectedNodeIds;
            if (selectedIds.Count == 0) return;

            _diagramClipboard = new ClipboardData();

            // 1. Копируем узлы
            _diagramClipboard.Nodes = _diagram.Nodes
                 .Where(n => selectedIds.Contains(n.Id))
                 .Select(n => new Node // Прямо здесь создаем новый экземпляр
                 {
                     Id = n.Id,
                     Title = n.Title,
                     X = n.X,
                     Y = n.Y,
                     ColorName = n.ColorName
                 })
                 .ToList();

            // 2. Копируем связи, которые соединяют ТОЛЬКО выбранные узлы
            _diagramClipboard.Edges = _diagram.Edges
                .Where(e => selectedIds.Contains(e.From) && selectedIds.Contains(e.To))
                .Select(e => new Edge { From = e.From, To = e.To }) // Копируем структуру
                .ToList();
            //_clipboard.Clear();
            //var selectedNodes = _diagram.Nodes.Where(n => _interaction.SelectedNodeIds.Contains(n.Id));
            //foreach (var node in selectedNodes)
            //{
            //    // Создаем глубокую копию, чтобы не менять оригиналы
            //    _clipboard.Add(new Node
            //    {
            //        Title = node.Title + " (копия)",
            //        X = node.X,
            //        Y = node.Y,
            //        ColorName = node.ColorName
            //    });
            //}
        }

        private void Paste()
        {
            if (_diagramClipboard == null || _diagramClipboard.Nodes.Count == 0) return;

            // Словарь для сопоставления старых ID из буфера с новыми ID в диаграмме
            var idMap = new Dictionary<Guid, Guid>();
            var newNodes = new List<Node>();
            var newEdges = new List<Edge>();

            // Шаг 1: Создаем новые узлы и заполняем карту ID
            foreach (var oldNode in _diagramClipboard.Nodes)
            {
                var newNode = oldNode.Clone();
                var oldId = newNode.Id;
                newNode.Id = Guid.NewGuid(); // Генерируем новый ID
                newNode.Title += " (копия)"; // Твое пожелание из списка придирок

                // Смещаем копию чуть в сторону, чтобы она не перекрыла оригинал
                newNode.X += 20;
                newNode.Y += 20;

                idMap[oldId] = newNode.Id;
                newNodes.Add(newNode);
            }

            // Шаг 2: Создаем новые связи, используя карту ID
            foreach (var oldEdge in _diagramClipboard.Edges)
            {
                if (idMap.ContainsKey(oldEdge.From) && idMap.ContainsKey(oldEdge.To))
                {
                    newEdges.Add(new Edge
                    {
                        From = idMap[oldEdge.From],
                        To = idMap[oldEdge.To]
                    });
                }
            }

            // Шаг 3: Обертываем это в команду для Undo
            var command = new PasteGroupCommand(_diagram, newNodes, newEdges);
            _commandManager.Execute(command);

            // Выделяем вставленные объекты для удобства
            _interaction.SelectedNodeIds.Clear();
            foreach (var n in newNodes) _interaction.SelectedNodeIds.Add(n.Id);

            canvas.Invalidate();
            //if (_clipboard.Count == 0) return;

            //_interaction.SelectedNodeIds.Clear();
            //foreach (var clone in _clipboard)
            //{
            //    var newNode = new Node
            //    {
            //        Id = Guid.NewGuid(), // Новый ID обязателен!
            //        Title = clone.Title,
            //        X = clone.X + 20, // Сдвиг, чтобы не слиплись
            //        Y = clone.Y + 20,
            //        ColorName = clone.ColorName
            //    };
            //    _diagram.Nodes.Add(newNode);
            //    _interaction.SelectedNodeIds.Add(newNode.Id);
            //}
            //canvas.Invalidate();
        }

        private void ShowEditBox(Node node)
        {
            // 1. Создаем TextBox
            var editBox = new TextBox
            {
                Text = node.Title,
                Font = new Font("Segoe UI", 10),
                Multiline = true, // Чтобы удобно вводить длинные названия
                TextAlign = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 2. Рассчитываем положение (TextBox должен быть поверх узла)
            // Используем Renderer, чтобы узнать точный размер узла
            using (var g = canvas.CreateGraphics())
            {
                var size = _renderer.CalculateNodeSize(g, node);
                editBox.Size = new Size((int)size.Width, (int)size.Height);

                // Пересчитываем координаты центра в верхний левый угол для TextBox
                var screenPos = CanvasToScreen(new PointF(node.X - size.Width / 2, node.Y - size.Height / 2));
                editBox.Location = new Point((int)screenPos.X, (int)screenPos.Y);
            }

            // 3. Добавляем его на холст
            canvas.Controls.Add(editBox);
            editBox.Focus();
            editBox.SelectAll();

            // 4. Логика завершения редактирования
            void CommitChange()
            {
                if (editBox.Text != node.Title)
                {
                    var command = new RenameNodeCommand(node, editBox.Text);
                    _commandManager.Execute(command);
                    _propertyGrid.Refresh(); // Обновляем таблицу свойств, если она открыта
                }
                Cleanup();
            }

            void Cleanup()
            {
                canvas.Controls.Remove(editBox);
                editBox.Dispose();
                canvas.Focus(); // Возвращаем фокус холсту для работы горячих клавиш
                canvas.Invalidate();
            }

            // 5. Обработка клавиш
            editBox.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; CommitChange(); }
                if (e.KeyCode == Keys.Escape) { Cleanup(); }
            };

            // Если пользователь кликнул мимо — сохраняем изменения
            editBox.LostFocus += (s, e) => CommitChange();
        }

        private PointF CanvasToScreen(PointF canvasPos)
        {
            // Если у тебя нет Zoom/Pan, то это просто canvasPos.
            // Если позже добавим Zoom, здесь нужно будет умножать на масштаб.
            //return canvasPos;
            return new PointF(canvasPos.X * _zoom + _panOffset.X, canvasPos.Y * _zoom + _panOffset.Y);
        }

        private float GetDistanceToSegment(PointF pt, PointF p1, PointF p2)
        {
            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;

            if (dx == 0 && dy == 0)
                return (float)Math.Sqrt((pt.X - p1.X) * (pt.X - p1.X) + (pt.Y - p1.Y) * (pt.Y - p1.Y));

            float t = ((pt.X - p1.X) * dx + (pt.Y - p1.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            float closestX = p1.X + t * dx;
            float closestY = p1.Y + t * dy;

            return (float)Math.Sqrt((pt.X - closestX) * (pt.X - closestX) + (pt.Y - closestY) * (pt.Y - closestY));
        }

        private Edge FindEdgeAt(PointF pt)
        {
            const float tolerance = 6.0f; // 6 пикселей - оптимально для клика мышкой

            foreach (var edge in _diagram.Edges)
            {
                var fromNode = _diagram.Nodes.Find(n => n.Id == edge.From);
                var toNode = _diagram.Nodes.Find(n => n.Id == edge.To);

                if (fromNode == null || toNode == null) continue;

                var p1 = new PointF(fromNode.X, fromNode.Y);
                var p2 = new PointF(toNode.X, toNode.Y);

                if (GetDistanceToSegment(pt, p1, p2) <= tolerance)
                {
                    return edge;
                }
            }
            return null;
        }
        //private void SaveDiagram()
        //{
        //    using (var sfd = new SaveFileDialog())
        //    {
        //        sfd.Filter = "Causal Diagram Files (*.json)|*.json";
        //        if (sfd.ShowDialog() == DialogResult.OK)
        //        {
        //            try
        //            {
        //                DiagramSerializer.SaveToFile(sfd.FileName, _diagram);
        //                MessageBox.Show("Диаграмма успешно сохранена!");
        //            }
        //            catch (Exception ex)
        //            {
        //                MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
        //            }
        //        }
        //    }
        //}

        //private void LoadDiagram()
        //{
        //    using (var ofd = new OpenFileDialog())
        //    {
        //        ofd.Filter = "Causal Diagram Files (*.json)|*.json";
        //        if (ofd.ShowDialog() == DialogResult.OK)
        //        {
        //            try
        //            {
        //                // Загружаем новую диаграмму
        //                var loadedDiagram = DiagramSerializer.LoadFromFile(ofd.FileName);

        //                // ВАЖНО: Обновляем ссылку в контроллере и форме
        //                // (Предполагается, что у тебя есть метод ResetDiagram или аналогичный)
        //                _diagram.Nodes.Clear();
        //                _diagram.Edges.Clear();
        //                _diagram.Nodes.AddRange(loadedDiagram.Nodes);
        //                _diagram.Edges.AddRange(loadedDiagram.Edges);

        //                _commandManager.Clear(); // Очищаем историю Undo/Redo при загрузке нового файла
        //                canvas.Invalidate();
        //                MessageBox.Show("Диаграмма загружена!");
        //            }
        //            catch (Exception ex)
        //            {
        //                MessageBox.Show($"Ошибка при загрузке: {ex.Message}");
        //            }
        //        }
        //    }
        //}
    }


}


