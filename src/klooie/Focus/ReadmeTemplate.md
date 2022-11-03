# Focus

## Focus Basics

In a Klooie application, zero or one control at a time can have focus. Having focus means a few things:

1. By default, any key press events will be directed to the control via it's KeyInputReceived event.
2. The control should use the themeable FocusColor and FocusContrastColor properties to alter its appearance, letting the user know which control has focus.

The user can press Tab to advance to the next focusable control. The user can press Shift + Tab to move focus to the previous focusable control.

You can programmatically focus a control by using the ConsoleControl.Focus() method. You can unfocus a control using the Unfocus() method.

You can run code when a particular control gets or loses focus by using the Focused and Unfocused events.

//#FocusEvents

## Global key handlers

You can use the ConsoleApp.PushKeyForLifetime(...) overloads to register a global keyboard handler for a given key. When a global handler is registered a focused control's KeyInputReceived event will not fire. If you wish to passively handle global key events while letting the focused control to the active handling then subscribe to the ConsoleApp.GlobalKeyPressed event.

Global key handlers registered before a dialog is shown will not be triggered until after the dialog is dismissed. See the Focus Stack section below to learn more.

//#PushKeyForLifetime

## Focus Stack

The Focus Stack is used to enable modal dialogs and dialog-like experiences. If you are using klooie's built-in dialogs then you should not have to deal with the focus stack directly. But you should be aware of how it works and see the warning below.

ConsoleApp exposes 2 powerful, yet dangerous methods.

```cs
ConsoleApp.PushFocusStack();
PushFocusStack.PopFocusStack();
```

Before displaying a dialog on the screen klooie calls ConsoleApp.PushFocusStack(). This will ensure that controls that were added on the lower layer(s) of the stack are unfocused and cannot be focused until the dialog is gone.

This creates an experience where the user can tab through the focusable controls on the dialog without the controls 'underneath' the dialog being included in the cycling.

After the dialog closes klooie calls ConsoleApp.PopFocusStack(). This will restore focus to the control that had it before the dialog was shown.

### Focus Stack Warning

When using dialogs or the focus stack more broadly you can get into trouble. Adding controls to containers below the dialog while the dialog is still alive will cause issues:

1. The user will be able to cycle to that control via the tab key. If that control is underneath the dialog then the user may think their keystroke was ignored. Furthermore, any additional keystrokes will be forwarded to the hidden control.
2. After the dialog closes, the user will not be able to tab to the control even if it's CanFocus property is set to true.

If you have logic like auto-refreshing panels below the dialog you may want to disable that functionality while dialogs are shown.
