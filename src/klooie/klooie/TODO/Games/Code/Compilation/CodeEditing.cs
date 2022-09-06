using PowerArgs;

namespace klooie.Gaming.Code;
public class BlockInfo
{
    public CodeControl Target { get; set; }
    public float BottomOfBlock { get; set; }
    public int BlockHeight { get; set; }
}

public static class CodeEditing
{
    public static IStatement FindStatementAboveLine(LineNumberControl lineElement)
    {
        var ret = CodeControl.CompiledCodeElements
          .Where(c => c.Top < lineElement.Top)
          .OrderBy(c => lineElement.Top - c.Top)
          .FirstOrDefault();
        return ret != null ? ret.Token.Statement : null;
    }

    public static void IndentLines(float y, int numberOfLines)
    {
        var codeToMove = CodeControl.CodeElements
            .Where(c => c.Top >= y && c.Top < y + numberOfLines);

        foreach (var codeElement in codeToMove)
        {
            codeElement.MoveBy(4, 0);
        }
    }

    public static void InsertLines(float y, int numLines, params GameCollider[] moveExclusions)
    {
        for (var i = 0; i < numLines; i++)
        {
            foreach (var element in Game.Current.GamePanel.Controls.WhereAs<GameCollider>().Where(c => c.Top >= y && c.HasSimpleTag(SpacialAwareness.PassThruTag) == false && c is LineNumberControl == false))
            {
                if (moveExclusions == null || moveExclusions.Contains(element) == false)
                {
                    element.MoveBy(0, 1);
                }
            }
        }
    }

    public static void ReplaceCompiledKeywordWithMalicuousOne(string replacement, CodeControl toReplace)
    {
        var lengthDelta = replacement.Length - toReplace.Token.Value.Length;
        var codeToMove = CodeControl.CodeElements
            .Where(c => c.Top == toReplace.Top && c.Left > toReplace.Left);

        foreach (var codeElement in codeToMove)
        {
            codeElement.MoveBy(lengthDelta, 0);
        }
        toReplace.Dispose();
        var replacementElement = Game.Current.GamePanel.Add(new MaliciousCodeElement(replacement));
        replacementElement.MoveTo(toReplace.Left, toReplace.Top);
    }

    public static CodeControl IdentifyNextRunningLine(int targetLine)
    {
        var target = CodeControl.CompiledCodeElements
             .Where(c => c.Token.Statement is RunningCodeStatement && c.Token == c.Token.Statement.Tokens.First())
             .Where(c => c.Token.Line >= targetLine)
             .OrderBy(c => c.Token.Line - targetLine)
             .FirstOrDefault();
        return target;
    }

    public static CodeControl IdentifyKeyword(List<CodeControl> exclusions, List<string> keywords, RectF proximityHint)
    {
        var keyword = CodeControl.CompiledCodeElements
         .Where(c => exclusions.Contains(c) == false)
         .Where(c => c.Token.Type == TokenType.Keyword)
         .Where(c => keywords.Count() == 0 || keywords.Contains(c.Token.Value))
         .OrderBy(c => c.CalculateDistanceTo(proximityHint))
         .FirstOrDefault();

        return keyword;
    }

    public static BlockInfo IdentifyBlock(List<CodeControl> exclusions, GameCollider proximityHint)
    {
        var currentTarget = CodeControl.CompiledCodeElements
         .Where(c => exclusions.Contains(c) == false)
         .Where(c => c.Token.Statement is RunningCodeStatement && c.Token == c.Token.Statement.Tokens.First())
         .OrderBy(c => c.CalculateDistanceTo(proximityHint))
         .FirstOrDefault();

        if (currentTarget == null)
        {
            return null;
        }

        var topOfBlock = currentTarget.Top;
        var currentTargetBottomOfBlock = currentTarget.Top;

        while (topOfBlock > 0)
        {
            var lineAboveInBlock = CodeControl.CodeElements
              .Where(c => c.Left == currentTarget.Left && c.Top == topOfBlock - 1 && c.Token.Statement is RunningCodeStatement && c.Token == c.Token.Statement.Tokens.First())
              .FirstOrDefault();

            if (lineAboveInBlock == null)
            {
                break;
            }
            else
            {
                topOfBlock--;
                currentTarget = lineAboveInBlock;
            }
        }
        var blockHeight = 1;

        while (currentTargetBottomOfBlock < Game.Current.GamePanel.Height - 1)
        {
            var lineBelowInBlock = CodeControl.CodeElements
              .Where(c => c.Left == currentTarget.Left && c.Top == topOfBlock + blockHeight && c.Token.Statement is RunningCodeStatement && c.Token == c.Token.Statement.Tokens.First())
              .FirstOrDefault();

            if (lineBelowInBlock == null)
            {
                break;
            }
            else
            {
                currentTargetBottomOfBlock++;
                blockHeight++;
            }
        }

        return new BlockInfo()
        {
            BlockHeight = blockHeight,
            BottomOfBlock = currentTargetBottomOfBlock,
            Target = currentTarget
        };
    }

    public static void RemoveLines(List<LineNumberControl> removedLineNumberElements)
    {
        foreach (var lineToRemove in removedLineNumberElements)
        {
            foreach (var codeElement in CodeControl.CompiledCodeElements.Where(c => Math.Abs(c.CenterY() - lineToRemove.CenterY()) < .2f))
            {
                codeElement.Dispose();
            }
        }

        foreach (var element in Game.Current.GamePanel.Controls.Where(e => e is LineNumberControl == false && e.CenterY() > removedLineNumberElements.First().CenterY()))
        {
            element.MoveBy(0, -removedLineNumberElements.Count);
        }
    }

    public static void RemoveLines(int y, int numLines, params GameCollider[] moveExclusions)
    {
        for (var i = 0; i < numLines; i++)
        {
            foreach (var element in Game.Current.GamePanel.Controls.Where(c => c.Top >= y && c is LineNumberControl == false))
            {
                if (moveExclusions == null || moveExclusions.Contains(element) == false)
                {
                    if (element.Top < y + numLines && element is CodeControl)
                    {
                        element.Dispose();
                    }
                    else
                    {
                        element.MoveBy(0, -1);
                    }
                }
            }
        }
    }
}