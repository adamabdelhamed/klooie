The types in this folder represent the built-in controls that you can use in your applications.

## Label

Labels are the most basic controls. They display text. By default, labels cannot be focused. If the Text property of the label is unstyled (default foreground and background color) then the control's Foreground and Background properties will be applied to the text. If the Text property is a styled ConsoleString then the text's style will be used.

//#LabelSample

## Button

A button can be 'pressed' with the enter key when it has focus. It also supports a shortcut key that can be pressed even when the button does not have focus.

//#ButtonSample

## ListViewer

The ListViewer control displays tabular data in a familiar table style with a pager control. It can receive focus via tab and the user can navigate rows using the up and down arrow keys. Here's how to use the ListViewer.

//#ListViewerSample

## Dropdown

A dropdown lets the user pick from a set of choices.

//#DropdownSample

## MinimumSizeShield

Sometimes you only want the user to see a control if it has enough space to display properly. A MinimumSizeShield is helpful in these cases.

Simply fill a container with a MinimumSizeShield and it will display itself along with a helpful message whenever the container is too small to show the other controls.

//#MinimumSizeShieldSample


## XY Chart

You can build complex controls with klooie. Here's how to use the built-in XYChart. This is useful when building quick command line apps that visualize data.

//#XYChartSample

## Custom controls

You can derive from ConsoleControl to create your own controls.

//#CustomControlSample