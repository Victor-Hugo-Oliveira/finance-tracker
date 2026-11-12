using System.Text;

namespace FinanceTracker.Core.Transfer;

/// <summary>
/// Minimal RFC 4180 CSV reader/writer: quoted fields, doubled quotes ("") and line breaks inside quotes.
/// The separator is configurable because Brazilian spreadsheets use ';' (the comma is the decimal mark).
/// </summary>
public static class Csv
{
    public static string Write(IEnumerable<IReadOnlyList<string>> rows, char separator = ';')
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Count; i++)
            {
                if (i > 0) sb.Append(separator);
                sb.Append(Escape(row[i], separator));
            }
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    private static string Escape(string value, char separator)
    {
        var needsQuotes = value.Contains(separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
                          || value.StartsWith(' ') || value.EndsWith(' ');
        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    /// <summary>Guesses the separator from the header line: whichever of ';', ',' or tab appears most.</summary>
    public static char DetectSeparator(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        char[] candidates = [';', ',', '\t'];
        return candidates.OrderByDescending(c => firstLine.Count(ch => ch == c)).First();
    }

    /// <summary>Parses the text into rows; each row remembers the line it started on (1-based) for error messages.</summary>
    public static List<(int Line, List<string> Fields)> Read(string text, char separator)
    {
        var rows = new List<(int, List<string>)>();
        if (text.Length > 0 && text[0] == '﻿') text = text[1..]; // BOM written by Excel

        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var rowStartLine = 1;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            // Skip completely empty lines (e.g. the trailing newline).
            if (!(fields.Count == 1 && fields[0].Length == 0))
                rows.Add((rowStartLine, fields));
            fields = [];
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }
            }
            else if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == separator)
            {
                EndField();
            }
            else if (c == '\r')
            {
                // Handled with the following '\n'; a lone '\r' also ends the row.
                if (i + 1 >= text.Length || text[i + 1] != '\n') { EndRow(); line++; rowStartLine = line; }
            }
            else if (c == '\n')
            {
                EndRow();
                line++;
                rowStartLine = line;
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0) EndRow();
        return rows;
    }
}
