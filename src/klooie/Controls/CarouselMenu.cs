using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace klooie
{
    public enum SelectedSlot { Left, Center, Right }

    public struct Shortcut
    {
        public ConsoleKey Key;
        public ConsoleModifiers? Modifier;
        public Shortcut(ConsoleKey key, ConsoleModifiers? mod = null) { Key = key; Modifier = mod; }
    }

    public class CarouselOptions
    {
        public float AnimationDuration { get; set; } = 200;
        public SelectedSlot SelectedSlot { get; set; } = SelectedSlot.Center;
        public Shortcut BackwardKey { get; set; } = new Shortcut(ConsoleKey.LeftArrow);
        public Shortcut ForwardKey { get; set; } = new Shortcut(ConsoleKey.RightArrow);
        public EasingFunction Easing { get; set; } = EasingFunctions.EaseInOut;
        public bool Wrap { get; set; } = true;
        // Optional hook: tweak visuals (e.g., emphasis) when an item occupies a slot.
        public Action<ConsoleControl, SelectedSlot, bool/*isVisible*/> OnStyleItem { get; set; }
    }

    /// <summary>
    /// Generic 3-slot carousel (Left/Center/Right). Selected item can live in any slot.
    /// Items are any ConsoleControl; we reuse the same child per index (no clones).
    /// </summary>
    public class CarouselMenu : ProtectedConsolePanel
    {
        public CarouselOptions Options { get; }
        public int Count => items.Count;
        public int SelectedIndex { get; private set; } = 0;

        public Event<int> SelectionChanged => _selectionChanged ??= Event<int>.Create();
        private Event<int> _selectionChanged;

        private readonly List<ConsoleControl> items = new();
        private readonly Dictionary<int, ConsoleControl> live = new(); // index -> child
        private ConsoleControl leftCtrl, centerCtrl, rightCtrl;

        private ConsoleControl leftPlaceHolder, centerPlaceHolder, rightPlaceHolder;
        private int seekLtLease;
        private Recyclable seekLt;

        public CarouselMenu(CarouselOptions options = null)
        {
            Options = options ?? new CarouselOptions();
            SetupInvisiblePlaceholders();
            BoundsChanged.Sync(Refresh, this);
            SetupKeyboardHandling();
        }

        #region Public API
        public void SetItems(IEnumerable<ConsoleControl> controls, int selectedIndex = 0)
        {
            // dispose old children we own
            foreach (var kv in live) kv.Value.TryDispose();
            items.Clear();
            live.Clear();
            leftCtrl = centerCtrl = rightCtrl = null;

            items.AddRange(controls ?? Enumerable.Empty<ConsoleControl>());
            SelectedIndex = items.Count == 0 ? 0 : Mod(selectedIndex, items.Count);
            Refresh();
        }

        public void AddItem(ConsoleControl control)
        {
            if (control == null) return;
            items.Add(control);
            if (items.Count == 1) SelectedIndex = 0;
            Refresh();
        }

        public void ClearItems() => SetItems(Array.Empty<ConsoleControl>());

        public void Select(int index, bool animate = false)
        {
            if (items.Count == 0) return;
            index = Mod(index, items.Count);
            if (index == SelectedIndex && !animate) return;

            if (!animate)
            {
                SelectedIndex = index;
                Refresh();
            }
            else
            {
                bool forward = AheadOf(SelectedIndex, index, items.Count);
                _ = SeekAsync(forward, Options.AnimationDuration, forceTarget: index);
            }
        }

        public void Next() => _ = SeekAsync(true, Options.AnimationDuration);
        public void Prev() => _ = SeekAsync(false, Options.AnimationDuration);
        #endregion

        #region Input
        private void SetupKeyboardHandling()
        {
            this.CanFocus = true;
            this.KeyInputReceived.Subscribe(key =>
            {
                var back = Options.BackwardKey;
                var fw = Options.ForwardKey;

                var backOk = (back.Modifier == null || key.Modifiers.HasFlag(back.Modifier.Value)) && key.Key == back.Key;
                var fwOk = (fw.Modifier == null || key.Modifiers.HasFlag(fw.Modifier.Value)) && key.Key == fw.Key;

                if (backOk) Prev();
                if (fwOk) Next();
            }, this);
        }
        #endregion

        #region Core
        private void SetupInvisiblePlaceholders()
        {
            var grid = ProtectedPanel.Add(new GridLayout("1%;15%;50%;15%;1%", "01%;25%;01%;46%;01%;25%;01%")).Fill();
            leftPlaceHolder = grid.Add(new ConsolePanel() { Background = RGB.Green }, 1, 2, 1, 1);
            centerPlaceHolder = grid.Add(new ConsolePanel() { Background = RGB.Green }, 3, 1, 1, 3);
            rightPlaceHolder = grid.Add(new ConsolePanel() { Background = RGB.Green }, 5, 2, 1, 1);
            grid.IsVisible = false;
        }

        private RectF LeftDest => MapBounds(leftPlaceHolder);
        private RectF CenterDest => MapBounds(centerPlaceHolder);
        private RectF RightDest => MapBounds(rightPlaceHolder);

        private RectF MapBounds(ConsoleControl ph) =>
            new RectF(ph.AbsoluteX - ProtectedPanel.AbsoluteX,
                      ph.AbsoluteY - ProtectedPanel.AbsoluteY,
                      ph.Width, ph.Height);

        private static int Mod(int x, int m) => ((x % m) + m) % m;

        private static bool AheadOf(int from, int to, int n)
        {
            // shortest forward path?
            var forward = Mod(to - from, n);
            var backward = Mod(from - to, n);
            return forward != 0 && forward <= backward;
        }

        private (int? l, int? c, int? r) MapIndices(int selected, int n)
        {
            if (n <= 0) return (null, null, null);
            if (n == 1)
            {
                return Options.SelectedSlot switch
                {
                    SelectedSlot.Left => (selected, null, null),
                    SelectedSlot.Center => (null, selected, null),
                    _ => (null, null, selected)
                };
            }
            if (n == 2)
            {
                var other = Mod(selected + 1, n);
                return Options.SelectedSlot switch
                {
                    SelectedSlot.Left => (selected, other, null),
                    SelectedSlot.Center => (null, selected, other),
                    _ => (other, null, selected)
                };
            }

            // n >= 3
            return Options.SelectedSlot switch
            {
                SelectedSlot.Left => (selected, Mod(selected + 1, n), Mod(selected + 2, n)),
                SelectedSlot.Center => (Mod(selected - 1, n), selected, Mod(selected + 1, n)),
                _ => (Mod(selected - 2, n), Mod(selected - 1, n), selected)
            };
        }

        private ConsoleControl EnsureChild(int index)
        {
            if (!live.TryGetValue(index, out var ctrl))
            {
                // Add the provided control as our child the first time we need it
                ctrl = ProtectedPanel.Add(items[index]);
                live[index] = ctrl;
            }
            ctrl.IsVisible = true;
            return ctrl;
        }

        private void StyleSlot(ConsoleControl c, SelectedSlot slot, bool visible)
            => Options.OnStyleItem?.Invoke(c, slot, visible);

        private void Place(ConsoleControl c, RectF dest, SelectedSlot slot)
        {
            c.Bounds = dest;
            StyleSlot(c, slot, true);
        }

        private RectF OffscreenFrom(RectF final, bool fromLeft)
            => new RectF(final.Left + (fromLeft ? -(final.Width + 2) : (final.Width + 2)), final.Top, final.Width, final.Height);

        private void HideIfNotNull(ConsoleControl c)
        {
            if (c == null) return;
            c.IsVisible = false;
            Options.OnStyleItem?.Invoke(c, SelectedSlot.Center, false); // "not visible" hint
        }

        private void Refresh()
        {
            if (Width == 0 || Height == 0) return;

            // kill any running animation for a crisp relayout
            seekLt?.TryDispose();

            // hide everything, then show what matters
            foreach (var kvp in live) HideIfNotNull(kvp.Value);

            if (items.Count == 0) return;

            var (li, ci, ri) = MapIndices(SelectedIndex, items.Count);
            leftCtrl = li.HasValue ? EnsureChild(li.Value) : null;
            centerCtrl = ci.HasValue ? EnsureChild(ci.Value) : null;
            rightCtrl = ri.HasValue ? EnsureChild(ri.Value) : null;

            if (leftCtrl != null) Place(leftCtrl, LeftDest, SelectedSlot.Left);
            if (centerCtrl != null) Place(centerCtrl, CenterDest, SelectedSlot.Center);
            if (rightCtrl != null) Place(rightCtrl, RightDest, SelectedSlot.Right);

            _selectionChanged?.Fire(SelectedIndex);
        }

        private async void Seek(bool forward, float duration) => await SeekAsync(forward, duration);

        public async Task<bool> SeekAsync(bool forward, float duration, int? forceTarget = null)
        {
            if (items.Count <= 1) return false;
            if (seekLt?.IsStillValid(seekLtLease) == true) return false;

            seekLt = this.CreateChildRecyclable(out seekLtLease);

            try
            {
                var n = items.Count;

                // Old mapping
                var (oldLIdx, oldCIdx, oldRIdx) = MapIndices(SelectedIndex, n);
                var oldL = oldLIdx.HasValue ? EnsureChild(oldLIdx.Value) : null;
                var oldC = oldCIdx.HasValue ? EnsureChild(oldCIdx.Value) : null;
                var oldR = oldRIdx.HasValue ? EnsureChild(oldRIdx.Value) : null;

                // New selection
                SelectedIndex = forceTarget ?? Mod(SelectedIndex + (forward ? 1 : -1), n);

                // New mapping
                var (newLIdx, newCIdx, newRIdx) = MapIndices(SelectedIndex, n);

                // Determine incoming index (present in new set but not in old set)
                var oldSet = new[] { oldLIdx, oldCIdx, oldRIdx }.Where(i => i.HasValue).Select(i => i.Value).ToHashSet();
                var newSet = new[] { newLIdx, newCIdx, newRIdx }.Where(i => i.HasValue).Select(i => i.Value).ToList();
                var incomingIdx = newSet.FirstOrDefault(i => !oldSet.Contains(i));
                var hasIncoming = newSet.Any(i => !oldSet.Contains(i));

                var incoming = hasIncoming ? EnsureChild(incomingIdx) : null;

                // Destinations
                var L = LeftDest; var C = CenterDest; var R = RightDest;

                // Ensure starting bounds for all participants
                if (oldL != null) oldL.Bounds = L;
                if (oldC != null) oldC.Bounds = C;
                if (oldR != null) oldR.Bounds = R;

                // Where does incoming start?
                if (incoming != null)
                {
                    var incomingFinal = R; // forward -> incoming to Right; backward -> Left
                    var fromLeft = !forward;
                    if (!forward) incomingFinal = L;

                    incoming.Bounds = OffscreenFrom(incomingFinal, fromLeft);
                    StyleSlot(incoming, forward ? SelectedSlot.Right : SelectedSlot.Left, true);
                }

                // Compute target slots for old visible controls based on universal shift pattern:
                // forward: oldL -> offL, oldC -> L, oldR -> C, incoming -> R
                // back   : oldR -> offR, oldC -> R, oldL -> C, incoming -> L
                var ease = Options.Easing;

                var tasks = new List<Task>();

                if (forward)
                {
                    if (oldL != null) tasks.Add(oldL.AnimateAsync(() => OffscreenFrom(L, true), duration, ease, animationLifetime: seekLt));
                    if (oldC != null) tasks.Add(oldC.AnimateAsync(() => L, duration, ease, animationLifetime: seekLt));
                    if (oldR != null) tasks.Add(oldR.AnimateAsync(() => C, duration, ease, animationLifetime: seekLt));
                    if (incoming != null) tasks.Add(incoming.AnimateAsync(() => R, duration, ease, animationLifetime: seekLt));
                }
                else
                {
                    if (oldR != null) tasks.Add(oldR.AnimateAsync(() => OffscreenFrom(R, false), duration, ease, animationLifetime: seekLt));
                    if (oldC != null) tasks.Add(oldC.AnimateAsync(() => R, duration, ease, animationLifetime: seekLt));
                    if (oldL != null) tasks.Add(oldL.AnimateAsync(() => C, duration, ease, animationLifetime: seekLt));
                    if (incoming != null) tasks.Add(incoming.AnimateAsync(() => L, duration, ease, animationLifetime: seekLt));
                }

                await Task.WhenAll(tasks);

                // Finalize: assign slot references and style
                leftCtrl = newLIdx.HasValue ? EnsureChild(newLIdx.Value) : null;
                centerCtrl = newCIdx.HasValue ? EnsureChild(newCIdx.Value) : null;
                rightCtrl = newRIdx.HasValue ? EnsureChild(newRIdx.Value) : null;

                if (leftCtrl != null) { leftCtrl.Bounds = L; StyleSlot(leftCtrl, SelectedSlot.Left, true); }
                if (centerCtrl != null) { centerCtrl.Bounds = C; StyleSlot(centerCtrl, SelectedSlot.Center, true); }
                if (rightCtrl != null) { rightCtrl.Bounds = R; StyleSlot(rightCtrl, SelectedSlot.Right, true); }

                // Hide non-visible
                foreach (var kv in live)
                {
                    if (kv.Value != leftCtrl && kv.Value != centerCtrl && kv.Value != rightCtrl)
                        HideIfNotNull(kv.Value);
                }

                _selectionChanged?.Fire(SelectedIndex);
                return true;
            }
            finally
            {
                seekLt?.TryDispose();
                seekLt = null;
            }
        }
        #endregion
    }
}
