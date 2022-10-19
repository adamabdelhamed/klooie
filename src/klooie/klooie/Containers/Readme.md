The types in this folder represent container controls that can be used to organize your application.

**ConsolePanel** is the base panel that you can use to add controls. When using ConsolePanel you must size and locate child controls yourself.

**ProtectedConsolePanel** is a panel that you can use to create a custom container where only the declaring type can add child controls.

**GridLayout** is a panel that lets you define columns and rows as pixels, percentages, and remainder values. It then lets you place controls in columns and rows with colspans and rowspans. Controls are sized and located automatically and adjust as the GridLayout is resized.

**Layout** is a static class that you can use to do basic layouts that are sufficient for many application. The helpers all maintain their semantics when the parent is resized.

Examples:

```cs

	// centers the label horizontally
	var label = someConsolePanel.Add(new Label("Hello".ToRed())).CenterHorizontally();

	// centers the label vertically
	var label = someConsolePanel.Add(new Label("Hello".ToRed())).CenterVertically();

	// centers the label vertically and horizontally
	var label = someConsolePanel.Add(new Label("Hello".ToRed())).CenterBoth();

	// Docks the label to the right
	var label = someConsolePanel.Add(new Label("Hello".ToRed())).DockToRight();

	// Docks the label to the right with some padding to the right
	var label = someConsolePanel.Add(new Label("Hello".ToRed())).DockToRight(padding: 4);

```