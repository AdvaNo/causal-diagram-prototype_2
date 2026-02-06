using System;
using System.ComponentModel; // Добавлено для BindingList
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CausalDiagram.Core.Models;

namespace CausalDiagram_1
{
    public class ForbiddenRulesForm : Form
    {
        private DataGridView _grid;
        private Button _btnOk;
        private Button _btnCancel;

        // Используем BindingList для автоматического обновления Grid при добавлении строк
        private BindingList<ForbiddenRule> _bindingRules;

        // Публичное свойство возвращает чистый List при запросе извне
        public List<ForbiddenRule> Rules => _bindingRules.ToList();

        public ForbiddenRulesForm(List<ForbiddenRule> rules)
        {
            // 1. Настройка формы
            Text = "Правила запрещённых связей";
            Width = 650; // Чуть шире для удобства
            Height = 450;
            StartPosition = FormStartPosition.CenterParent; // Открывать по центру
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            // 2. Подготовка данных (создаем копию, чтобы не менять оригинал до нажатия ОК)
            var safeList = rules != null ? new List<ForbiddenRule>(rules) : new List<ForbiddenRule>();
            _bindingRules = new BindingList<ForbiddenRule>(safeList);

            // 3. Инициализация UI
            InitializeControls();
        }

        private void InitializeControls()
        {
            // --- Grid ---
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, // Заполняет все пространство, кроме панели кнопок
                AutoGenerateColumns = false,
                BackgroundColor = System.Drawing.SystemColors.ControlLight,
                AllowUserToAddRows = true, // Разрешаем добавлять через UI
                AllowUserToDeleteRows = true
            };

            // Колонка "От"
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                DataSource = Enum.GetValues(typeof(NodeCategory)),
                HeaderText = "От категории",
                DataPropertyName = "FromCategory",
                Width = 150
            });

            // Колонка "К"
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                DataSource = Enum.GetValues(typeof(NodeCategory)),
                HeaderText = "К категории",
                DataPropertyName = "ToCategory",
                Width = 150
            });

            // Колонка "Причина"
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Причина запрета",
                DataPropertyName = "Reason",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill // Растягивается
            });

            _grid.DataSource = _bindingRules;

            // --- Кнопки ---
            _btnOk = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Width = 100, Height = 30 };
            _btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 100, Height = 30 };

            // Обработчики не обязательны, если задан DialogResult, но для надежности оставим
            _btnOk.Click += (s, e) => Close();
            _btnCancel.Click += (s, e) => Close();

            // --- Панель ---
            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft, // Кнопки справа
                Padding = new Padding(10),
                BackColor = System.Drawing.SystemColors.Control
            };

            bottomPanel.Controls.Add(_btnCancel);
            bottomPanel.Controls.Add(_btnOk); // Порядок добавления важен для RightToLeft

            // Добавляем на форму
            Controls.Add(_grid);
            Controls.Add(bottomPanel);
        }
    }
}