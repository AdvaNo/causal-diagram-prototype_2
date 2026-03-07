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
using System.IO;// Для работы с файлами
using System.Text.Json; // Для JSON
using System.Xml.Serialization; // Для XML



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

        // Новые сервисы 
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

        // Путь к текущему файлу
        private string _currentFilePath = null;

        // Путь на случай эксренных сохранений
        private ToolStripStatusLabel lblStatusInfo;
        private readonly string _autosavePath = Path.Combine(Path.GetTempPath(), "causal_recovery.json");

        //статус диаграммы
        StatusStrip statusStrip;

        //Сетка
        private bool _showGrid = false;
        private const int GridStep = 20;

        //для кнопок при Новом открытии
        private ToolStripButton btnSelect; 
        private ToolStripButton btnAdd;
        private ToolStripButton btnSave;
        private ToolStripButton btnLoad;
        private ToolStripButton btnConnect;
        private ToolStripButton btnDelete;
        private ToolStripButton btnUndo;
        private ToolStripButton btnRedo;
        private ToolStripButton newFileButton;

        //статусы зума и наличия
        private ToolStripStatusLabel lblCount;
        private ToolStripStatusLabel lblZoom;

        //для превью стрелок
        private PointF _currentMouseWorldPos;

        // Поле для хранения ссылки на активный редактор при двойной клик
        private TextBox _currentEditBox = null; 

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
            btnSelect = new ToolStripButton("Выбрать") { CheckOnClick = true, Checked = true };
            btnSelect.Click += (s, e) => SetMode(btnSelect, EditorMode.Select);
            //btnSelect.Click += (s, e) => _interaction.CurrentMode = EditorMode.Select;

            // 3. Кнопка "Добавить узел"
            btnAdd = new ToolStripButton("Новый узел") { CheckOnClick = true };
            btnAdd.Click += (s, e) => SetMode(btnAdd, EditorMode.AddNode);
            //btnAdd.Click += (s, e) => _interaction.CurrentMode = EditorMode.AddNode;

            //// 4. Логика взаимоисключения кнопок (чтобы нельзя было выбрать обе сразу)
            //btnSelect.CheckedChanged += (s, e) => { if (btnSelect.Checked) btnAdd.Checked = false; };
            //btnAdd.CheckedChanged += (s, e) => { if (btnAdd.Checked) btnSelect.Checked = false; };
            //btnSelect.Click += (s, e) => SetMode(btnSelect);
            //btnAdd.Click += (s, e) => SetMode(btnAdd);
            // Добавляем всё на панель
            //toolStrip.Items.AddRange(new ToolStripItem[] { btnSelect, btnAdd, new ToolStripSeparator(), btnSave, btnLoad });
            // Добавляем панель в форму (она сама прилипнет к верху)
            //this.Controls.Add(toolStrip);

            // 1. Создаем кнопку
            btnConnect = new ToolStripButton("Провести связь") { CheckOnClick = true };
            btnConnect.Click += (s, e) => SetMode(btnConnect, EditorMode.Connect);
            //btnConnect.Click += (s, e) => _interaction.CurrentMode = EditorMode.Connect;
            // 2. Добавляем её в логику взаимоисключения (чтобы только одна кнопка была нажата)
            //btnConnect.CheckedChanged += (s, e) => {
            //    if (btnConnect.Checked) { btnSelect.Checked = false; btnAdd.Checked = false; }
            //};
            // Не забудь добавить проверку для других кнопок, чтобы они выключали btnConnect!
            //btnSelect.CheckedChanged += (s, e) => { if (btnSelect.Checked) { btnAdd.Checked = false; btnConnect.Checked = false; } };
            //btnAdd.CheckedChanged += (s, e) => { if (btnAdd.Checked) { btnSelect.Checked = false; btnConnect.Checked = false; } };
            // 3. Добавь кнопку на панель
            //toolStrip.Items.Insert(2, btnConnect); // Вставляем после кнопки "Новый узел"

            btnDelete = new ToolStripButton("Удалить");
            btnDelete.Click += (s, e) => DeleteSelectedItems();

            //btnDelete.CheckedChanged += (s, e) => {
            //    if (btnConnect.Checked) { btnSelect.Checked = false; btnAdd.Checked = false; }
            //};
            //toolStrip.Items.Insert(3, btnDelete);

            btnUndo = new ToolStripButton("Отменить");
            btnUndo.Click += (s, e) => { _commandManager.Undo(); canvas.Invalidate(); };
            //btnUndo.CheckedChanged += (s, e) => {
            //    if (btnConnect.Checked) { btnSelect.Checked = false; btnAdd.Checked = false; }
            //};
            //toolStrip.Items.Insert(4, btnUndo);

            // 5. Кнопки Сохранить/Загрузить
            btnSave = new ToolStripButton("Сохранить");
            btnSave.Click += (s, e) => SaveProject();

            btnLoad = new ToolStripButton("Загрузить");
            btnLoad.Click += (s, e) => LoadProject();

            newFileButton = new ToolStripButton("Новый файл") { CheckOnClick = true };
            newFileButton.Click += (s, e) => NewDiagram();
            //toolStrip.Items.Insert(5, newFileButton);

            toolStrip.Items.AddRange(new ToolStripItem[] {
                btnSelect, btnAdd, btnConnect,
                new ToolStripSeparator(),
                btnDelete, btnUndo,
                new ToolStripSeparator(),
                btnSave, btnLoad, newFileButton
            });

            this.Controls.Add(toolStrip);

            //var btnGrid = new ToolStripButton("Сетка") { CheckOnClick = true, Checked = _showGrid };
            //btnGrid.Click += (s, e) => {
            //    _showGrid = btnGrid.Checked;
            //    canvas.Invalidate(); // Перерисовать, чтобы сетка исчезла или появилась
            //};
            //toolStrip.Items.Add(btnGrid);

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

            statusStrip = new StatusStrip();
            lblCount = new ToolStripStatusLabel { Text = "Узлов: 0, Связей: 0" };
            lblZoom = new ToolStripStatusLabel { Text = "Зум: 100%"/*, Alignment = ToolStripItemAlignment.Right */};
            //зум справа => добавим "пружину" (пустой лейбл, который съедает место)
            var spring = new ToolStripStatusLabel { Spring = true };

            statusStrip.Items.AddRange(new ToolStripItem[] { lblCount, spring, lblZoom });
            this.Controls.Add(statusStrip);

            //canvas.Paint += Canvas_Paint;
            this.Controls.Add(canvas);

            canvas.Paint += (s, e) => {

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
                        _renderer.Render(g, _diagram, _interaction.SelectedNodeIds, _selectedEdge, _zoom, _panOffset, _showGrid, GridStep);

                        g.ResetTransform();
                    }

                    // 4. Отрисовываем готовый буфер на реальный экран (без задержек и теней)
                    e.Graphics.DrawImage(buffer, 0, 0);
                }
            };

            // Подписываемся на мышь
            canvas.MouseDown += (s, e) => {
                // Если текстовое поле существует и оно активно (видимо)
                if (_currentEditBox != null)
                {
                    // При клике мимо оно само закроется через LostFocus (CommitChange),
                    // но нам нужно остановить дальнейшую обработку клика:
                    canvas.Focus(); // Забираем фокус у TextBox, провоцируя LostFocus
                    return; // ВЫХОДИМ, чтобы не создать новый узел
                }

                var p = ScreenToCanvas(e.Location);

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
                _currentMouseWorldPos = ScreenToCanvas(e.Location); // Запоминаем позицию мыши в координатах холста

                if (_interaction.CurrentMode == EditorMode.Connect && _interaction.StartNodeForConnection != null)
                {
                    _interaction.UpdateTempConnection(p);
                    canvas.Invalidate();
                }
                else
                {
                    _interaction.HandleMouseMove(p, canvas.ClientSize);
                    if (_showGrid && _interaction._primaryDraggedNode != null)
                    {
                        // Округляем координаты всех выделенных узлов (если тащим группу) 
                        // или только главного перетаскиваемого узла
                        foreach (var nodeId in _interaction.SelectedNodeIds)
                        {
                            var node = _diagram.Nodes.Find(n => n.Id == nodeId);
                            if (node != null)
                            {
                                node.X = (float)Math.Round(node.X / GridStep) * GridStep;
                                node.Y = (float)Math.Round(node.Y / GridStep) * GridStep;
                            }
                        }
                    }
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
                            MessageBox.Show("Связь между этими узлами уже существует!");
                        }
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
                        // Создаем команду 
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
                _renderer.Render(e.Graphics, _diagram, _interaction.SelectedNodeIds, _selectedEdge, _zoom, _panOffset, _showGrid, GridStep);

                //превью стрелок
                if (_interaction.CurrentMode == EditorMode.Connect && _interaction.StartNodeForConnection != null)
                {
                    // Берем узел напрямую из контроллера
                    var startNode = _interaction.StartNodeForConnection;

                    using (Pen tempPen = new Pen(Color.Gray, 2f) { DashStyle = DashStyle.Dash })
                    {
                        // Смещаем начальную точку в центр узла. 
                        // 75 и 40 — это примерные значения (половина ширины и высоты узла). 
                        // Если твои узлы другого размера, подставь сюда нужные цифры.
                        e.Graphics.DrawLine(tempPen, startNode.X + 15, startNode.Y + 10, _currentMouseWorldPos.X, _currentMouseWorldPos.Y);
                    }
                }

                e.Graphics.ResetTransform(); // Сбрасываем, чтобы UI (если есть) не уплыл
                
                // 5. Обновляем цифры в статус-баре (чтобы они менялись плавно во время действий)
                UpdateStatusIndicators();

                // 4. Рисуем элементы интерфейса поверх всего (Мини-карта)
                DrawMinimap(e.Graphics);                
            };

            canvas.MouseWheel += (s, e) => {
                float zoomStep = 1.1f;
                float oldZoom = _zoom;

                if (e.Delta > 0) _zoom *= zoomStep;
                else _zoom /= zoomStep;

                UpdateStatusBar();

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

            this.FormClosing += (s, e) => {
                if (!PromptToSave())
                {
                    e.Cancel = true; // Останавливаем закрытие
                }
            };
            this.Load += (s, e) => CheckForAutosaveRecovery();
        }

        //Не даст закрыть приложение, пока не было сохранения
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!PromptToSave())
            {
                e.Cancel = true; // Отменяем закрытие формы
            }
        }

        // Вспомогательный метод для перевода координат экрана в координаты диаграммы
        private PointF ScreenToCanvas(Point p)
        {
            return new PointF(
                (p.X - _panOffset.X) / _zoom,
                (p.Y - _panOffset.Y) / _zoom
            );
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            RenderDiagram(e.Graphics);
        }

        private void SaveProject()
        {
            //using (var sfd = new SaveFileDialog())
            //{
            //    sfd.Filter = "Causal Diagram Files (*.json)|*.json";
            //    if (sfd.ShowDialog() == DialogResult.OK)
            //    {
            //        try
            //        {
            //            DiagramSerializer.SaveToFile(sfd.FileName, _diagram);
            //            MessageBox.Show("Диаграмма успешно сохранена!");
            //        }
            //        catch (Exception ex)
            //        {
            //            MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            //        }
            //    }
            //}
            using (var sfd = new SaveFileDialog())
            {
                // 1. Добавляем выбор всех форматов в одно окно
                sfd.Filter = "JSON Diagram (*.json)|*.json|XML Diagram (*.xml)|*.xml|PNG Image (*.png)|*.png|SVG Vector (*.svg)|*.svg";
                sfd.Title = "Сохранить проект или экспортировать как изображение";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string ext = Path.GetExtension(sfd.FileName).ToLower();
                    try
                    {
                        switch (ext)
                        {
                            case ".json":
                                // Используем твой сериализатор или встроенный
                                string json = JsonSerializer.Serialize(_diagram, new JsonSerializerOptions { WriteIndented = true });
                                File.WriteAllText(sfd.FileName, json);
                                _currentFilePath = sfd.FileName; // Запоминаем путь
                                _commandManager.ResetModified(); // Сбрасываем "звездочку"
                                break;

                            case ".xml":
                                string xml = SerializeToXml(_diagram); 
                                File.WriteAllText(sfd.FileName, xml);
                                _currentFilePath = sfd.FileName;
                                _commandManager.ResetModified();
                                break;

                            case ".png":
                                // Вызываем экспорт в картинку (логика из предыдущего сообщения)
                                ExportToImageInternal(sfd.FileName);
                                break;
                            case ".svg":
                                string svgData = ExportToSvg();
                                File.WriteAllText(sfd.FileName, svgData);
                                _commandManager.ResetModified();
                                break;
                        }

                        UpdateTitle();
                        MessageBox.Show($"Успешно сохранено в формате {ext.ToUpper()}");
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
            //using (var ofd = new OpenFileDialog())
            //{
            //    ofd.Filter = "Causal Diagram Files (*.json)|*.json";
            //    if (ofd.ShowDialog() == DialogResult.OK)
            //    {
            //        try
            //        {
            //            // Загружаем новую диаграмму
            //            var loadedDiagram = DiagramSerializer.LoadFromFile(ofd.FileName);

            //            // ВАЖНО: Обновляем ссылку в контроллере и форме
            //            _diagram.Nodes.Clear();
            //            _diagram.Edges.Clear();
            //            _diagram.Nodes.AddRange(loadedDiagram.Nodes);
            //            _diagram.Edges.AddRange(loadedDiagram.Edges);

            //            _commandManager.Clear(); // Очищаем историю Undo/Redo при загрузке нового файла
            //            canvas.Invalidate();
            //            MessageBox.Show("Диаграмма загружена!");
            //        }
            //        catch (Exception ex)
            //        {
            //            MessageBox.Show($"Ошибка при загрузке: {ex.Message}");
            //        }
            //    }
            //}

            // 2. Сначала спрашиваем: "А вы сохранили текущую работу?"
            if (!PromptToSave()) return; // Если нажата "Отмена" — выходим из метода

            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Диаграммы (*.json, *.xml)|*.json;*.xml|Все файлы (*.*)|*.*";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string content = File.ReadAllText(ofd.FileName);
                        string ext = Path.GetExtension(ofd.FileName).ToLower();
                        Diagram loadedDiagram = null;

                        // 3. Логика чтения в зависимости от расширения
                        if (ext == ".json")
                        {
                            loadedDiagram = JsonSerializer.Deserialize<Diagram>(content);
                        }
                        else if (ext == ".xml")
                        {
                            loadedDiagram = DeserializeFromXml(content);
                        }

                        if (loadedDiagram != null)
                        {
                            // Обновляем текущую диаграмму
                            _diagram.Nodes.Clear();
                            _diagram.Edges.Clear();
                            _diagram.Nodes.AddRange(loadedDiagram.Nodes);
                            _diagram.Edges.AddRange(loadedDiagram.Edges);

                            _currentFilePath = ofd.FileName;
                            _commandManager.Clear(); // Очищаем историю правок

                            UpdateTitle();
                            canvas.Invalidate();
                            MessageBox.Show("Диаграмма успешно загружена!");
                        }
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
                // Передаем  список Guid
                var nodeIdsToDelete = _interaction.SelectedNodeIds.ToList();

                var command = new DeleteNodeCommand(_diagram, nodeIdsToDelete);
                _commandManager.Execute(command);

                _interaction.SelectedNodeIds.Clear();
                canvas.Invalidate();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
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
                CopySelected();
                return true;
            }

            // --- Вставка (Ctrl + V) ---
            if (keyData == (Keys.Control | Keys.V))
            {
                Paste();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        private void SetMode(ToolStripButton activeMode, EditorMode mode)
        {
            _interaction.CurrentMode = mode;
            // Список всех кнопок-режимов
            var modeButtons = new[] {btnSelect, btnAdd, btnConnect};

            foreach (var btn in modeButtons)
            {
                if (btn != null) btn.Checked = (btn == activeMode);
            }
        }

        private void RenderDiagram(Graphics g)
        {
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
            _propertyGrid.PropertySort = PropertySort.Categorized; 
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
        }

        private void ShowEditBox(Node node)
        {
            // Если вдруг уже что-то редактируется, закрываем старое (на всякий случай)
            if (_currentEditBox != null) Cleanup();

            // 1. Создаем TextBox
            _currentEditBox = new TextBox
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
                _currentEditBox.Size = new Size((int)size.Width, (int)size.Height);

                // Пересчитываем координаты центра в верхний левый угол для TextBox
                var screenPos = CanvasToScreen(new PointF(node.X - size.Width / 2, node.Y - size.Height / 2));
                _currentEditBox.Location = new Point((int)screenPos.X, (int)screenPos.Y);
            }

            // 3. Добавляем его на холст
            canvas.Controls.Add(_currentEditBox);
            _currentEditBox.Focus();
            _currentEditBox.SelectAll();

            // 4. Логика завершения редактирования
            void CommitChange()
            {
                if (_currentEditBox == null) return;

                if (_currentEditBox.Text != node.Title)
                {
                    var command = new RenameNodeCommand(node, _currentEditBox.Text);
                    _commandManager.Execute(command);
                    _propertyGrid.Refresh(); // Обновляем таблицу свойств, если она открыта
                }
                Cleanup();
            }

            void Cleanup()
            {
                //canvas.Controls.Remove(_currentEditBox);
                //_currentEditBox.Dispose();
                //canvas.Focus(); // Возвращаем фокус холсту для работы горячих клавиш
                //canvas.Invalidate();

                if (_currentEditBox == null) return;

                var boxToDispose = _currentEditBox;
                _currentEditBox = null; // Сначала зануляем ссылку!


                //canvas.Controls.Remove(_currentEditBox);
                //_currentEditBox.Dispose();
                //_currentEditBox = null; // ОСВОБОЖДАЕМ ПОЛЕ
                //canvas.Focus();
                //canvas.Invalidate();



                canvas.Controls.Remove(boxToDispose);
                boxToDispose.Dispose();

                canvas.Focus();
                canvas.Invalidate();
            }

            // 5. Обработка клавиш
            _currentEditBox.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; CommitChange(); }
                if (e.KeyCode == Keys.Escape) { Cleanup(); }
            };

            // Если пользователь кликнул мимо — сохраняем изменения
            _currentEditBox.LostFocus += (s, e) => CommitChange();
        }

        private PointF CanvasToScreen(PointF canvasPos)
        {
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

        private void UpdateTitle()
        {
            string fileName = _currentFilePath == null ? "Новая диаграмма" : Path.GetFileName(_currentFilePath);
            string modifiedMark = _commandManager.IsModified ? "*" : "";
            this.Text = $"Causal Editor - {fileName}{modifiedMark}";
        }
        private bool PromptToSave()
        {
            if (!_commandManager.IsModified || (_diagram.Nodes.Count == 0 && _diagram.Edges.Count == 0)) return true; // Изменений нет, можно идти дальше

            var result = MessageBox.Show(
                "В текущую диаграмму внесены изменения. Сохранить их?",
                "Сохранение",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                return Save(); // Пытаемся сохранить
            }
            else if (result == DialogResult.No)
            {
                _commandManager.ResetModified();
                return true; // Пользователь не хочет сохранять, но мы можем продолжать
            }
            else
            {
                return false; // Нажата "Отмена", прерываем операцию
            }
        }

        //новый файл
        private void NewDiagram()
        {
            if (!PromptToSave()) return;

            _diagram = new Diagram();
            _currentFilePath = null;
            _commandManager.Clear();
            _interaction.SelectedNodeIds.Clear();

            SetMode(btnSelect, EditorMode.Select);
            UpdateStatusBar();
            UpdateTitle();
            canvas.Invalidate();
        }

        //открыть имеющуюся диаграмму
        private void OpenDiagram()
        {
            if (!PromptToSave()) return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON Files (*.json)|*.json|XML Files (*.xml)|*.xml|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string content = File.ReadAllText(openFileDialog.FileName);
                        string extension = Path.GetExtension(openFileDialog.FileName).ToLower();

                        if (extension == ".json")
                        {
                            _diagram = JsonSerializer.Deserialize<Diagram>(content);
                        }
                        else if (extension == ".xml")
                        {
                            var serializer = new XmlSerializer(typeof(Diagram));
                            using (var reader = new StringReader(content))
                            {
                                _diagram = (Diagram)serializer.Deserialize(reader);
                            }
                        }

                        _currentFilePath = openFileDialog.FileName;
                        _commandManager.Clear(); // Очищаем историю после загрузки нового файла
                        _interaction.SelectedNodeIds.Clear();

                        UpdateTitle();
                        canvas.Invalidate();
                    }
                    catch (JsonException)
                    {
                        MessageBox.Show("Файл поврежден или имеет неверный формат.", "Ошибка чтения", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при открытии: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private bool PerformSave(string path)
        {
            try
            {
                string extension = Path.GetExtension(path).ToLower();
                string data = "";

                if (extension == ".json")
                {
                    // Настройка, чтобы JSON был читаемым (в столбик), а не одной строкой
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    data = JsonSerializer.Serialize(_diagram, options);
                }
                else if (extension == ".xml")
                {
                    data = SerializeToXml(_diagram);
                }

                File.WriteAllText(path, data);

                _currentFilePath = path;
                _commandManager.ResetModified(); // Сбрасываем "звездочку" в заголовке

                // Удаляем файл автосохранения, так как данные теперь в безопасности
                if (File.Exists(_autosavePath))
                {
                    try { File.Delete(_autosavePath); }
                    catch { /* Игнорируем, если файл занят другим процессом */ }
                }

                UpdateStatusBar();
                UpdateTitle();

                if (lblStatusInfo != null) lblStatusInfo.Text = $"Файл сохранен: {DateTime.Now:HH:mm:ss}";

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }


        }

        private bool Save()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                return SaveAs();
            }
            return PerformSave(_currentFilePath);
        }

        private bool SaveAs()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "JSON Files (*.json)|*.json|XML Files (*.xml)|*.xml";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return PerformSave(saveFileDialog.FileName);
                }
            }
            return false;
        }

        private string SerializeToXml(Diagram diagram)
        {
            var serializer = new XmlSerializer(typeof(Diagram));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, diagram);
                return writer.ToString();
            }
        }
        private Diagram DeserializeFromXml(string xmlText)
        {
            var serializer = new XmlSerializer(typeof(Diagram));
            using (var reader = new StringReader(xmlText))
            {
                return (Diagram)serializer.Deserialize(reader);
            }
        }
        private void ExportToImageInternal(string fileName)
        {
            if (_diagram.Nodes.Count == 0) return;

            // 1. Считаем реальный размер схемы, чтобы на картинке не было пустоты
            float minX = _diagram.Nodes.Min(n => n.X) - 40;
            float minY = _diagram.Nodes.Min(n => n.Y) - 40;
            float maxX = _diagram.Nodes.Max(n => n.X + 170); // 150 ширина + запас
            float maxY = _diagram.Nodes.Max(n => n.Y + 100); // 80 высота + запас

            int width = Math.Max(1, (int)(maxX - minX));
            int height = Math.Max(1, (int)(maxY - minY));

            // 2. Рисуем на виртуальном холсте (Bitmap)
            using (Bitmap bmp = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White); // Фон
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    // Смещаем камеру так, чтобы самый левый верхний узел был в углу картинки
                    g.TranslateTransform(-minX, -minY);

                    // Используем твой стандартный рендерер (без выделения узлов)
                    _renderer.Render(g, _diagram, new HashSet<Guid>(), null, 1.0f, _panOffset, _showGrid, GridStep);
                }

                // 3. Сохраняем файл
                bmp.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        private string ExportToSvg()
        {
            // Используем StringBuilder для быстрой сборки текста
            var svg = new System.Text.StringBuilder();

            // Вычисляем границы
            float minX = _diagram.Nodes.Count > 0 ? _diagram.Nodes.Min(n => n.X) - 20 : 0;
            float minY = _diagram.Nodes.Count > 0 ? _diagram.Nodes.Min(n => n.Y) - 20 : 0;
            float width = _diagram.Nodes.Count > 0 ? _diagram.Nodes.Max(n => n.X + 170) - minX : 800;
            float height = _diagram.Nodes.Count > 0 ? _diagram.Nodes.Max(n => n.Y + 100) - minY : 600;

            // Заголовок SVG
            svg.AppendLine($"<svg width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" xmlns=\"http://www.w3.org/2000/svg\">");
            svg.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"white\" />"); // Фон
            svg.AppendLine($"<g transform=\"translate({-minX}, {-minY})\">"); // Сдвиг координат

            // 1. Рисуем связи (Edges)
            foreach (var edge in _diagram.Edges)
            {
                var from = _diagram.Nodes.FirstOrDefault(n => n.Id == edge.From);
                var to = _diagram.Nodes.FirstOrDefault(n => n.Id == edge.To);
                if (from != null && to != null)
                {
                    // Упрощенная линия (можно добавить стрелочки через marker-end)
                    svg.AppendLine($"  <line x1=\"{from.X + 75}\" y1=\"{from.Y + 40}\" x2=\"{to.X + 75}\" y2=\"{to.Y + 40}\" stroke=\"black\" stroke-width=\"2\" />");
                }
            }

            // 2. Рисуем узлы (Nodes)
            foreach (var node in _diagram.Nodes)
            {
                string color = node.ColorName.ToString() ?? "LightBlue";
                svg.AppendLine($"  <rect x=\"{node.X}\" y=\"{node.Y}\" width=\"150\" height=\"80\" rx=\"10\" fill=\"{color}\" stroke=\"black\" stroke-width=\"1\" />");
                svg.AppendLine($"  <text x=\"{node.X + 75}\" y=\"{node.Y + 45}\" font-family=\"Arial\" font-size=\"12\" text-anchor=\"middle\" fill=\"black\">{node.Title}</text>");
            }

            svg.AppendLine("</g>");
            svg.AppendLine("</svg>");

            return svg.ToString();
        }

        private bool ValidateDiagram()
        {
            // Ищем связи, у которых удалили один из узлов
            var brokenEdges = _diagram.Edges.Where(e =>
                !_diagram.Nodes.Any(n => n.Id == e.From) ||
                !_diagram.Nodes.Any(n => n.Id == e.To)).ToList();

            if (brokenEdges.Any())
            {
                var result = MessageBox.Show(
                    $"Найдено {brokenEdges.Count} битых связей. Удалить их перед сохранением?",
                    "Валидация", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    foreach (var edge in brokenEdges) _diagram.Edges.Remove(edge);
                    return true;
                }
                return false; // Пользователь не хочет сохранять "битый" файл
            }
            return true;
        }

        private void InitAutosave()
        {
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 120000; // 2 минуты (в миллисекундах)
            timer.Tick += (s, e) =>
            {
                // Сохраняем, только если схема не пуста и есть изменения
                if (_commandManager.IsModified && _diagram.Nodes.Count > 0)
                {
                    try
                    {
                        string json = JsonSerializer.Serialize(_diagram);
                        File.WriteAllText(_autosavePath, json);

                        // Делаем файл скрытым (опционально, для чистоты)
                        File.SetAttributes(_autosavePath, FileAttributes.Hidden);

                        // Можно тихонько обновить статус-бар, чтобы ты видела, что это работает
                        lblStatusInfo.Text = $"Автосохранение: {DateTime.Now:HH:mm}";
                    }
                    catch { /* Если не удалось сохранить в темп, просто игнорируем */ }
                }
            };
            timer.Start();
        }

        private void CheckForAutosaveRecovery()
        {
            if (File.Exists(_autosavePath))
            {
                var result = MessageBox.Show(
                    "Программа была закрыта некорректно. Восстановить последнюю автосохраненную диаграмму?",
                    "Восстановление",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        string json = File.ReadAllText(_autosavePath);
                        _diagram = JsonSerializer.Deserialize<Diagram>(json);
                        _commandManager.Clear(); // Сбрасываем историю, чтобы начать с чистого листа
                        canvas.Invalidate();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось восстановить файл: {ex.Message}");
                    }
                }

                // В любом случае удаляем файл автосохранения после предложения, 
                // чтобы не спрашивать при каждом запуске
                File.Delete(_autosavePath);
            }
        }

        private void UpdateStatusBar()
        {
            if (lblStatusInfo == null) return;

            // Обновляем текст счетчиков
            lblCount.Text = $"Узлов: {_diagram.Nodes.Count} | Связей: {_diagram.Edges.Count}";

            // Обновляем текст зума
            lblZoom.Text = $"Зум: {Math.Round(_zoom * 100)}%";
        }
        private void DrawMinimap(Graphics g)
        {
            // 1. Показываем только при отдалении
            //if (_zoom >= 0.99f) return;
            if (_diagram.Nodes.Count == 0) return; // Если узлов нет, рисовать нечего

            int miniW = 200;
            int miniH = 150;
            int margin = 10;
            Rectangle miniRect = new Rectangle(margin, canvas.Height - miniH - margin - 30, miniW, miniH);

            // 2. ВЫЧИСЛЯЕМ ГРАНИЦЫ ВСЕЙ СХЕМЫ (Bounding Box)
            float minX = _diagram.Nodes.Min(n => n.X);
            float minY = _diagram.Nodes.Min(n => n.Y);
            float maxX = _diagram.Nodes.Max(n => n.X + 150); // + ширина узла
            float maxY = _diagram.Nodes.Max(n => n.Y + 80);  // + высота узла

            float diagramW = maxX - minX;
            float diagramH = maxY - minY;

            // 3. ВЫЧИСЛЯЕМ МАСШТАБ, чтобы вписать схему в мини-карту
            // Берем минимальный коэффициент, чтобы влезло и по ширине, и по высоте
            float scaleX = (miniW - 10) / diagramW;
            float scaleY = (miniH - 10) / diagramH;
            float fitScale = Math.Min(scaleX, scaleY);

            // Ограничиваем сверху, чтобы на одном узле карта не растягивалась на весь экран
            if (fitScale > 0.15f) fitScale = 0.15f;

            // 4. РИСУЕМ ПОДЛОЖКУ
            g.FillRectangle(new SolidBrush(Color.FromArgb(200, Color.White)), miniRect);
            g.DrawRectangle(Pens.LightGray, miniRect);

            var state = g.Save();
            g.SetClip(miniRect);

            // 5. ТРАНСФОРМАЦИЯ
            // Сначала двигаем начало координат мини-карты
            g.TranslateTransform(miniRect.X + 5, miniRect.Y + 5);
            // Масштабируем
            g.ScaleTransform(fitScale, fitScale);
            // Сдвигаем всё рисование так, чтобы самый левый верхний узел был в (0,0) мини-карты
            g.TranslateTransform(-minX, -minY);

            // 6. РЕНДЕР (без выделения и сетки)
            _renderer.Render(g, _diagram, new HashSet<Guid>(), null, 1.0f, new PointF(0, 0), false, 20);

            using (Pen viewPen = new Pen(Color.FromArgb(180, Color.DodgerBlue), 1f))
            using (SolidBrush viewBrush = new SolidBrush(Color.FromArgb(20, Color.DodgerBlue)))
            {
                // 1. Вычисляем, что видит пользователь на основном экране
                // Мы инвертируем трансформацию основной камеры:
                // Ширина видимой области = ширина канваса / зум
                float viewWidth = canvas.Width / _zoom;
                float viewHeight = canvas.Height / _zoom;
                // Начало видимой области (с учетом панорамирования)
                float viewX = -_panOffset.X / _zoom;
                float viewY = -_panOffset.Y / _zoom;

                float paddingX = viewWidth * 0.05f;
                float paddingY = viewHeight * 0.05f;

                float slimX = viewX + paddingX;
                float slimY = viewY + paddingY;
                float slimW = viewWidth - (paddingX * 2);
                float slimH = viewHeight - (paddingY * 2);

                // 2. Рисуем эту рамку. 
                // Поскольку мы уже внутри ScaleTransform и TranslateTransform мини-карты,
                // нам просто нужно передать координаты основного "окна"
                g.FillRectangle(viewBrush, slimX, slimY, slimW, slimH);

                // Рисуем контур, используя именно уменьшенные (slim) координаты
                g.DrawRectangle(viewPen, slimX, slimY, slimW, slimH);
            }
            g.Restore(state);
        }

        private void UpdateStatusIndicators()
        {
            // Безопасная проверка: если лейблы еще не созданы, ничего не делаем
            if (lblCount == null || lblZoom == null) return;

            // Обновляем текст
            lblCount.Text = $"Узлов: {_diagram.Nodes.Count} | Связей: {_diagram.Edges.Count}";
            lblZoom.Text = $"Зум: {Math.Round(_zoom * 100)}%";
        }
    }
}


