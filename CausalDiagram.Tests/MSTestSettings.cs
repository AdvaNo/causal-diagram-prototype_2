using CausalDiagram.Core.Models;
using CausalDiagram.Core.Services;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CausalDiagram.Core.Commands;
using CausalDiagram.Commands;

[TestClass]
public class AnalyzerTests
{
    [TestMethod]
    public void Analyze_ChainOfTwoNodes_ReturnsRootCause()
    {
        // 1. ARRANGE (Подготовка данных)
        var diagram = new Diagram();
        var nodeA = new Node { Id = Guid.NewGuid(), Title = "Причина" };
        var nodeB = new Node { Id = Guid.NewGuid(), Title = "Следствие" };
        diagram.Nodes.Add(nodeA);
        diagram.Nodes.Add(nodeB);
        diagram.Edges.Add(new Edge { From = nodeA.Id, To = nodeB.Id });

        // 2. ACT (Выполнение действия)
        var result = TraceAnalyzer.Analyze(diagram, nodeB);

        // 3. ASSERT (Проверка результата)
        Assert.AreEqual(1, result.RootCauses.Count);
        Assert.AreEqual("Причина", result.RootCauses[0].Title);
    }
    [TestMethod]
    public void Node_Clone_CreatesExactCopy()
    {
        // 1. ARRANGE (Подготовка)
        var originalNode = new Node
        {
            Title = "Оригинал",
            X = 100,
            Y = 200,
            ColorName = NodeColor.Red // Предполагается, что Red есть в твоем enum
        };

        // 2. ACT (Действие)
        var clonedNode = originalNode.Clone();

        // 3. ASSERT (Проверка)
        Assert.IsNotNull(clonedNode, "Клон не должен быть null");
        Assert.AreEqual(originalNode.Id, clonedNode.Id, "ID должны совпадать при клонировании");
        Assert.AreEqual(originalNode.Title, clonedNode.Title, "Названия не совпадают");
        Assert.AreEqual(originalNode.X, clonedNode.X, "Координата X не совпадает");
        Assert.AreEqual(originalNode.Y, clonedNode.Y, "Координата Y не совпадает");
        Assert.AreEqual(originalNode.ColorName, clonedNode.ColorName, "Цвета не совпадают");
    }

    [TestMethod]
    public void Edge_Creation_AssignsNewGuid()
    {
        // Действие
        var edge1 = new Edge();
        var edge2 = new Edge();

        // Проверка
        Assert.AreNotEqual(Guid.Empty, edge1.Id);
        Assert.AreNotEqual(edge1.Id, edge2.Id, "Каждая новая связь должна иметь уникальный ID");
    }

    [TestMethod]
    public void TraceResult_Constructor_AddsTargetToPathNodeIds()
    {
        // Подготовка
        var targetNode = new Node { Title = "Цель" };

        // Действие
        var result = new TraceResult(targetNode);

        // Проверка
        Assert.IsTrue(result.PathNodeIds.Contains(targetNode.Id), "Целевой узел должен быть добавлен в подсвечиваемые узлы при создании");
    }

    [TestMethod]
    public void TraceResult_HasCauses_ReturnsCorrectBoolean()
    {
        // Подготовка
        var result = new TraceResult(new Node());

        // Проверка 1: изначально пусто
        Assert.IsFalse(result.HasCauses, "Изначально причин быть не должно");

        // Действие
        result.RootCauses.Add(new Node());

        // Проверка 2: добавили причину
        Assert.IsTrue(result.HasCauses, "Свойство должно вернуть true, если есть первопричины");
    }
    // Вспомогательный класс-заглушка для тестов
    private class DummyCommand : ICommand
    {
        public int ExecuteCount { get; private set; } = 0;
        public int UndoCount { get; private set; } = 0;

        public void Execute() => ExecuteCount++;
        public void Undo() => UndoCount++;
    }

    [TestMethod]
    public void ExecuteCommand_AddsToUndoStack_SetsModifiedFlag()
    {
        // Arrange
        var manager = new CommandManager();
        var cmd = new DummyCommand();

        // Act
        manager.Execute(cmd);

        // Assert
        Assert.AreEqual(1, cmd.ExecuteCount, "Команда должна быть выполнена 1 раз");
        Assert.IsTrue(manager.CanUndo, "Стек Undo должен содержать команду");
        Assert.IsFalse(manager.CanRedo, "Стек Redo должен быть пустым после новой команды");
        Assert.IsTrue(manager.IsModified, "Флаг изменений должен стать true");
    }

    [TestMethod]
    public void Undo_MovesCommandToRedoStack_CallsUndoOnCommand()
    {
        // Arrange
        var manager = new CommandManager();
        var cmd = new DummyCommand();
        manager.Execute(cmd); // Выполнили команду
        manager.ResetModified(); // Сбросили флаг, чтобы проверить, поднимет ли его Undo

        // Act
        manager.Undo();

        // Assert
        Assert.AreEqual(1, cmd.UndoCount, "Метод Undo у команды должен быть вызван 1 раз");
        Assert.IsFalse(manager.CanUndo, "Стек Undo должен стать пустым");
        Assert.IsTrue(manager.CanRedo, "Команда должна переместиться в стек Redo");
        Assert.IsTrue(manager.IsModified, "Флаг изменений должен стать true после отмены");
    }

    [TestMethod]
    public void Redo_MovesCommandBackToUndo_CallsExecuteAgain()
    {
        // Arrange
        var manager = new CommandManager();
        var cmd = new DummyCommand();
        manager.Execute(cmd); // cmd.ExecuteCount = 1
        manager.Undo();       // переместили в Redo

        // Act
        manager.Redo();

        // Assert
        Assert.AreEqual(2, cmd.ExecuteCount, "Команда должна быть выполнена повторно (итого 2 раза)");
        Assert.IsTrue(manager.CanUndo, "Команда должна вернуться в Undo");
        Assert.IsFalse(manager.CanRedo, "Стек Redo должен опустеть");
    }

    [TestMethod]
    public void PushToUndoStack_AddsWithoutExecuting()
    {
        // Arrange
        var manager = new CommandManager();
        var cmd = new DummyCommand();

        // Act
        manager.PushToUndoStack(cmd);

        // Assert
        Assert.AreEqual(0, cmd.ExecuteCount, "Команда НЕ должна быть выполнена");
        Assert.IsTrue(manager.CanUndo, "Но команда должна оказаться в стеке Undo");
    }

    [TestMethod]
    public void Clear_EmptiesAllStacks_ResetsModifiedFlag()
    {
        // Arrange
        var manager = new CommandManager();
        manager.Execute(new DummyCommand());

        // Act
        manager.Clear();

        // Assert
        Assert.IsFalse(manager.CanUndo);
        Assert.IsFalse(manager.CanRedo);
        Assert.IsFalse(manager.IsModified, "После очистки флаг изменений должен сброситься");
    }
}
