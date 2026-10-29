namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// Aligns the columns of the GFM pipe table under the cursor (plan MD-006, EDT-002).
/// </summary>
public static partial class TableFormatter
{
    private const int MinimumWidth = 3;

    /// <summary>
    /// Formats the pipe table that contains <paramref name="offset"/>.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="offset">A source offset inside the table.</param>
    /// <returns>One edit replacing the table, or <see langword="null"/> if the offset is not in a pipe table.</returns>
    public static TextEdit? Format(
        string source,
        int offset)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (offset < 0 || offset > source.Length)
        {
            return null;
        }

        var lines = SplitLines(source);
        if (FindTable(source, lines, offset) is not var (first, last))
        {
            return null;
        }

        var indent = LeadingWhitespace().Match(Text(source, lines[first])).Value;
        var rows = new List<List<string>>();
        for (var i = first; i <= last; i++)
        {
            rows.Add(SplitCells(Text(source, lines[i]).Trim()));
        }

        var eol = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return new TextEdit(lines[first].Start, lines[last].End, Render(rows, indent, eol));
    }

    /// <summary>
    /// Finds the first and last line of the table around the offset; the second line must be a delimiter row.
    /// </summary>
    private static (int First, int Last)? FindTable(
        string source,
        List<(int Start, int End)> lines,
        int offset)
    {
        var current = lines.FindIndex(l => offset >= l.Start && offset <= l.End);
        if (current < 0 || !IsTableLine(source, lines[current]))
        {
            return null;
        }

        var first = current;
        while (first > 0 && IsTableLine(source, lines[first - 1]))
        {
            first--;
        }

        var last = current;
        while (last < lines.Count - 1 && IsTableLine(source, lines[last + 1]))
        {
            last++;
        }

        return last - first >= 1 && DelimiterRow().IsMatch(Text(source, lines[first + 1]))
            ? (first, last)
            : null;
    }

    private static string Render(
        List<List<string>> rows,
        string indent,
        string eol)
    {
        var delimiters = rows[1];
        var columns = rows.Max(r => r.Count);
        var widths = new int[columns];
        Array.Fill(widths, MinimumWidth);
        foreach (var row in rows.Where((_, index) => index != 1))
        {
            for (var col = 0; col < row.Count; col++)
            {
                widths[col] = Math.Max(widths[col], row[col].Length);
            }
        }

        var sb = new StringBuilder();
        for (var index = 0; index < rows.Count; index++)
        {
            if (index > 0)
            {
                sb.Append(eol);
            }

            sb.Append(indent).Append('|');
            for (var col = 0; col < columns; col++)
            {
                var delimiter = col < delimiters.Count ? delimiters[col] : "---";
                var cell = index == 1
                    ? Delimiter(delimiter, widths[col])
                    : Pad(col < rows[index].Count ? rows[index][col] : string.Empty, widths[col], delimiter);
                sb.Append(' ').Append(cell).Append(" |");
            }
        }

        return sb.ToString();
    }

    private static List<(int Start, int End)> SplitLines(string source)
    {
        var lines = new List<(int Start, int End)>();
        var start = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != '\n')
            {
                continue;
            }

            var end = i > start && source[i - 1] == '\r' ? i - 1 : i;
            lines.Add((start, end));
            start = i + 1;
        }

        lines.Add((start, source.Length));
        return lines;
    }

    private static string Text(
        string source,
        (int Start, int End) line)
        => source[line.Start..line.End];

    private static bool IsTableLine(
        string source,
        (int Start, int End) line)
    {
        var text = Text(source, line);
        return text.Contains('|', StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// Splits a row on unescaped pipes outside code spans, dropping the optional outer pipes.
    /// </summary>
    private static List<string> SplitCells(string row)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var inCode = false;
        var escaped = false;
        foreach (var c in row)
        {
            if (escaped)
            {
                sb.Append(c);
                escaped = false;
            }
            else if (c == '\\')
            {
                sb.Append(c);
                escaped = true;
            }
            else if (c == '`')
            {
                inCode = !inCode;
                sb.Append(c);
            }
            else if (c == '|' && !inCode)
            {
                cells.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        cells.Add(sb.ToString().Trim());
        if (row.StartsWith('|'))
        {
            cells.RemoveAt(0);
        }

        if (row.EndsWith('|') && !row.EndsWith("\\|", StringComparison.Ordinal) && cells.Count > 0)
        {
            cells.RemoveAt(cells.Count - 1);
        }

        return cells;
    }

    /// <summary>
    /// Pads a cell to the column width following the column's alignment: right-aligned columns pad on the
    /// left, centered columns on both sides (the extra space on the right), others on the right.
    /// </summary>
    private static string Pad(
        string cell,
        int width,
        string delimiter)
    {
        var padding = width - cell.Length;
        return (delimiter.StartsWith(':'), delimiter.EndsWith(':')) switch
        {
            (true, true) => new string(' ', padding / 2) + cell + new string(' ', padding - (padding / 2)),
            (false, true) => cell.PadLeft(width),
            _ => cell.PadRight(width),
        };
    }

    /// <summary>
    /// Rebuilds a delimiter cell at the given width, keeping its alignment markers.
    /// </summary>
    private static string Delimiter(
        string original,
        int width)
        => (original.StartsWith(':'), original.EndsWith(':')) switch
        {
            (true, true) => ":" + new string('-', width - 2) + ":",
            (false, true) => new string('-', width - 1) + ":",
            (true, false) => ":" + new string('-', width - 1),
            _ => new string('-', width),
        };

    [GeneratedRegex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DelimiterRow();

    [GeneratedRegex(@"^\s*", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LeadingWhitespace();
}