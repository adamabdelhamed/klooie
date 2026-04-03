## TerminalHost

This folder contains terminal host implementations that connect layout resizing and final presentation to specific console backends.

- Keep resize behavior compatible with the active presentation mode; ANSI hosts should use ANSI-safe clearing/blanking.
- Put terminal-specific debounce and resize-settling logic here rather than in layout or control code.
- Avoid relying on native console buffer operations when the host is presenting through ANSI frame writes.
