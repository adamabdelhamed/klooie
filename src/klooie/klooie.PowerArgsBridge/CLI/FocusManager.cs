﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerArgs.Cli
{
    /// <summary>
    /// A class that manages the focus of a CLI application
    /// </summary>
    public class FocusManager : ObservableObject
    {
        /// <summary>
        /// Data object used to capture the focus context on the stack
        /// </summary>
        public class FocusContext
        {
            public KeyboardInterceptionManager Interceptors { get; private set; } = new KeyboardInterceptionManager();

            /// <summary>
            /// The controls being managed by this context
            /// </summary>
            public List<ConsoleControl> Controls { get; internal set; }

            /// <summary>
            /// The current focus index within this context
            /// </summary>
            public int FocusIndex { get; internal set; }

            /// <summary>
            /// Creates a new focus context
            /// </summary>
            public FocusContext()
            {
                Controls = new List<ConsoleControl>();
                FocusIndex = -1;
            }
        }

        private Stack<FocusContext> focusStack;

        public Stack<FocusContext> Stack => focusStack;

        /// <summary>
        /// Gets the number of layers on the stack
        /// </summary>
        public int StackDepth
        {
            get
            {
                return focusStack.Count;
            }
        }

        /// <summary>
        /// Gets the keyboard manager that can be used to intercept keystrokes on the current layer
        /// </summary>
        public KeyboardInterceptionManager GlobalKeyHandlers
        {
            get
            {
                return focusStack.Peek().Interceptors;
            }
        }

        /// <summary>
        /// Gets the currently focused control or null if there is no control with focus yet.
        /// </summary>
        public ConsoleControl FocusedControl
        {
            get
            {
                return Get<ConsoleControl>();
            }
            private set
            {
                Set(value);
            }
        }

        /// <summary>
        /// Initializes the focus manager
        /// </summary>
        public FocusManager()
        {
            focusStack = new Stack<FocusContext>();
            focusStack.Push(new FocusContext());
        }

        /// <summary>
        /// Adds the current control to the current focus context
        /// </summary>
        /// <param name="c">The control to add</param>
        internal void Add(ConsoleControl c)
        {
            if(focusStack.Peek().Controls.Contains(c))
            {
                throw new InvalidOperationException("Item already being tracked");
            }
            focusStack.Peek().Controls.Add(c);
        }

        /// <summary>
        /// Removes the control from all focus contexts
        /// </summary>
        /// <param name="c">The control to remove</param>
        internal void Remove(ConsoleControl c)
        {
            foreach(var context in focusStack)
            {
                context.Controls.Remove(c);
            }
        }

        /// <summary>
        /// Pushes a new focus context onto the stack.  This is useful, for example, when a dialog appears above all other
        /// controls and you want to limit focus to the dialog to acheive a modal affect.  You must remember to call pop
        /// when your context ends.
        /// </summary>
        public void Push()
        {
            focusStack.Push(new FocusContext());
            FirePropertyChanged(nameof(StackDepth));
        }

        /// <summary>
        /// Pops the current focus context.  This should be called if you've implemented a modal dialog like experience and your dialog
        /// has just closed.  Pop() will automatically restore focus on the previous context.
        /// </summary>
        public void Pop()
        {
            if(focusStack.Count == 1)
            {
                throw new InvalidOperationException("Cannot pop the last item off the focus stack");
            }

            var context = focusStack.Pop();
            TryRestoreFocus();
            FirePropertyChanged(nameof(StackDepth));
        }

        /// <summary>
        /// Tries to set focus on the given control.
        /// </summary>
        /// <param name="newFocusControl">the control to focus.  </param>
        /// <returns>True if the focus was set or if it was already set, false if the control cannot be focused</returns>
        public bool TrySetFocus(ConsoleControl newFocusControl)
        {
            var index = focusStack.Peek().Controls.IndexOf(newFocusControl);
            if (index < 0)
            {
                return false;
            }

            if(newFocusControl.CanFocus == false)
            {
                return false;
            }
            else  if(newFocusControl == FocusedControl)
            {
                return true;
            }
            else
            {
                var oldFocusedControl = FocusedControl;
                if (oldFocusedControl != null)
                {
                    oldFocusedControl.HasFocus = false;
                }
                newFocusControl.HasFocus = true;
                FocusedControl = newFocusControl;

                focusStack.Peek().FocusIndex = index;

                if (oldFocusedControl != null)
                {
                    oldFocusedControl.FireFocused(false);
                }

                if (FocusedControl != null)
                {
                    FocusedControl.FireFocused(true);
                }
                return true;
            }
        }

        /// <summary>
        /// Tries to move the focus forward or backwards
        /// </summary>
        /// <param name="forward">If true then the manager will try to move forwards, otherwise backwards</param>
        /// <returns>True if the focus moved, false otehrwise</returns>
        public bool TryMoveFocus(bool forward = true)
        {
            if (focusStack.Peek().Controls.Count == 0)
            {
                return false;
            }

            int initialPosition = focusStack.Peek().FocusIndex;

            DateTime start = DateTime.Now;
            do
            {
                bool wrapped = CycleFocusIndex(forward);
                var nextControl = focusStack.Peek().Controls[focusStack.Peek().FocusIndex];
                if(nextControl.CanFocus && nextControl.TabSkip == false)
                {
                    return TrySetFocus(nextControl);
                }

                if (wrapped && initialPosition < 0) break;
            }
            while (focusStack.Peek().FocusIndex != initialPosition && DateTime.Now - start < TimeSpan.FromSeconds(.2));

            return false;
        }

        /// <summary>
        /// Tries to restore the focus on the given context
        /// </summary>
        /// <returns>True if the focus changed, false otehrwise</returns>
        public bool TryRestoreFocus()
        {
            if (focusStack.Peek().Controls.Where(c => c.CanFocus).Count() == 0)
            {
                return false;
            }

            int initialPosition = focusStack.Peek().FocusIndex;

            bool skipOnce = true;
            do
            {
                bool wrapped = false;
                if (skipOnce)
                {
                    skipOnce = false;
                }
                else
                {
                    wrapped = CycleFocusIndex(true);
                }

                var newFocusIndex = Math.Max(0, Math.Min(focusStack.Peek().FocusIndex, focusStack.Peek().Controls.Count - 1));
                focusStack.Peek().FocusIndex = newFocusIndex;
                var nextControl = focusStack.Peek().Controls[focusStack.Peek().FocusIndex];
                if (nextControl.CanFocus)
                {
                    return TrySetFocus(nextControl);
                }

                if (wrapped && initialPosition < 0) break;
            }
            while (focusStack.Peek().FocusIndex != initialPosition);

            return false;
        }

        /// <summary>
        /// Clears the focus, but preserves the focus index
        /// </summary>
        public void ClearFocus()
        {
            if (ConsoleApp.Current?.ShouldContinue == false) return;
            if (FocusedControl != null)
            {
                FocusedControl.HasFocus = false;
                FocusedControl.FireFocused(false);
                FocusedControl = null;
            }
        }

        private bool CycleFocusIndex(bool forward)
        {
            if (forward)
            {
                focusStack.Peek().FocusIndex++;
            }
            else
            {
                focusStack.Peek().FocusIndex--;
            }

            if (focusStack.Peek().FocusIndex >= focusStack.Peek().Controls.Count)
            {
                focusStack.Peek().FocusIndex = 0;
                return true;
            }
            else if (focusStack.Peek().FocusIndex < 0)
            {
                focusStack.Peek().FocusIndex = focusStack.Peek().Controls.Count - 1;
                return true;
            }

            return false;
        }
    }
}
