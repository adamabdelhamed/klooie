/*
using PowerArgs;
using PowerArgs.Cli;
using PowerArgs.Cli.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
namespace CLIborg
{
    public class CodeReviewElement : SpacialElement_REPLACE
    {
        public const ConsoleColor AddedForegroundColor = ConsoleColor.Black;
        public const ConsoleColor AddedBackgroundColor = ConsoleColor.Green;

        public const ConsoleColor RemovedForegroundColor = ConsoleColor.Black;
        public const ConsoleColor RemovedBackgroundColor = ConsoleColor.Red;


        public ConsoleBitmap Visual { get; private set; }

        public CodeReviewElement(List<LineNumberControl> addedLines, List<LineNumberControl> removedLines)
        {
            var orderedLines = addedLines.Union(removedLines).OrderBy(l => l.Line).ToList();

            var left = orderedLines.First().Left;
            var top = orderedLines.OrderBy(l => l.Top).First();
            var width = (int)Math.Ceiling(Game.Current.CameraPanel.Width - left);
            var height = orderedLines.Count;
            ResizeTo(width, height);
            MoveTo(left, orderedLines.First().Top, ZIndexes.CodeReviewZIndex);

            Visual = new ConsoleBitmap(width, height);

            var bitmapY = 0;
            foreach (var lineElement in orderedLines)
            {
                Visual.FillRect(new ConsoleCharacter(' ', null, addedLines.Contains(lineElement) ? AddedBackgroundColor : RemovedBackgroundColor), 0, bitmapY, Visual.Width, 1);
                Visual.DrawString(lineElement.Line.ToString().ToConsoleString(addedLines.Contains(lineElement) ? AddedForegroundColor : RemovedForegroundColor, addedLines.Contains(lineElement) ? AddedBackgroundColor : RemovedBackgroundColor), 0, bitmapY);

                foreach (var codeElement in CodeControl.CodeElements.Where(c => Math.Abs(c.CenterY() - lineElement.CenterY()) < .5f).OrderBy(c => c.Left))
                {
                    var bitmapXStart = ConsoleMath.Round(codeElement.Left - left);

                    var code = codeElement.LineOfCode;
                    for (var i = 0; i < code.Length; i++)
                    {
                        var c = new ConsoleCharacter(code[i].Value, addedLines.Contains(lineElement) ? AddedForegroundColor : RemovedForegroundColor, addedLines.Contains(lineElement) ? AddedBackgroundColor : RemovedBackgroundColor);
                        Visual.DrawPoint(c, bitmapXStart + i, bitmapY);
                    }
                }

                bitmapY++;
            }
        }
    }

    [SpacialElement_REPLACEBinding(typeof(CodeReviewElement))]
    public class CodeReviewElementRenderer : SpacialElement_REPLACERenderer
    {
        public CodeReviewElement CodeReviewElement => Element as CodeReviewElement;
        private ConsoleBitmapViewer viewer;
        public CodeReviewElementRenderer()
        {
            this.TransparentBackground = true;
        }

        public override void OnBind()
        {
            viewer = Add(new ConsoleBitmapViewer() { Bitmap = CodeReviewElement.Visual }).Fill();
        }
    }
}
*/