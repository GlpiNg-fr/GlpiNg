using System.Text;

namespace GlpiNg.Web.Api.HighLevel;

/// <summary>Erreur de syntaxe RSQL, avec le message de GLPI.</summary>
public sealed class RsqlException(string message) : Exception(message);

/// <summary>Nœud d'une condition RSQL : comparaison, ou combinaison ET / OU.</summary>
public abstract record RsqlNode;

public sealed record RsqlComparison(string Property, string Operator, string? Value) : RsqlNode;

public sealed record RsqlLogical(bool IsAnd, IReadOnlyList<RsqlNode> Items) : RsqlNode;

/// <summary>
/// Filtres RSQL de l'API v2 (paramètre <c>filter</c>) : portage du lexer de GLPI
/// (Glpi\Api\HL\RSQL\Lexer) et d'un analyseur qui respecte la priorité SQL que GLPI obtient en
/// concaténant les conditions (ET avant OU, parenthèses).
/// </summary>
public static class Rsql
{
    private enum T
    {
        And,
        Or,
        Open,
        Close,
        Property,
        Operator,
        Value,
        Unspecified,
    }

    public static readonly string[] Operators =
    [
        "==", "!=", "=in=", "=out=", "=lt=", "=le=", "=gt=", "=ge=", "=like=", "=ilike=",
        "=isnull=", "=notnull=", "=empty=", "=notempty=", "=notlike=", "=notilike=",
    ];

    public static bool ExpectsValue(string op) => op is not ("=isnull=" or "=notnull=" or "=empty=" or "=notempty=");

    public static RsqlNode? Parse(string query)
    {
        List<(T Type, string Value)> tokens = Tokenize(query);
        int position = 0;
        RsqlNode? node = ParseOr(tokens, ref position);
        return node;
    }

    private static RsqlNode? ParseOr(List<(T Type, string Value)> tokens, ref int position)
    {
        List<RsqlNode> items = [];
        RsqlNode? first = ParseAnd(tokens, ref position);
        if (first is not null)
        {
            items.Add(first);
        }
        while (position < tokens.Count && tokens[position].Type == T.Or)
        {
            position++;
            RsqlNode? next = ParseAnd(tokens, ref position);
            if (next is not null)
            {
                items.Add(next);
            }
        }
        return items.Count switch { 0 => null, 1 => items[0], _ => new RsqlLogical(false, items) };
    }

    private static RsqlNode? ParseAnd(List<(T Type, string Value)> tokens, ref int position)
    {
        List<RsqlNode> items = [];
        RsqlNode? first = ParsePrimary(tokens, ref position);
        if (first is not null)
        {
            items.Add(first);
        }
        while (position < tokens.Count && tokens[position].Type == T.And)
        {
            position++;
            RsqlNode? next = ParsePrimary(tokens, ref position);
            if (next is not null)
            {
                items.Add(next);
            }
        }
        return items.Count switch { 0 => null, 1 => items[0], _ => new RsqlLogical(true, items) };
    }

    private static RsqlNode? ParsePrimary(List<(T Type, string Value)> tokens, ref int position)
    {
        if (position >= tokens.Count)
        {
            return null;
        }
        (T type, string value) = tokens[position];
        if (type == T.Open)
        {
            position++;
            RsqlNode? inner = ParseOr(tokens, ref position);
            if (position < tokens.Count && tokens[position].Type == T.Close)
            {
                position++;
            }
            return inner;
        }
        if (type == T.Property)
        {
            string property = value;
            position++;
            string op = position < tokens.Count && tokens[position].Type == T.Operator ? tokens[position++].Value : string.Empty;
            string? operand = null;
            if (position < tokens.Count && tokens[position].Type is T.Value or T.Unspecified)
            {
                operand = tokens[position].Type == T.Value ? tokens[position].Value : null;
                position++;
            }
            if (operand is not null && operand.Length >= 2
                && ((operand[0] == '"' && operand[^1] == '"') || (operand[0] == '\'' && operand[^1] == '\'')))
            {
                operand = operand[1..^1];
            }
            return new RsqlComparison(property, op, operand);
        }
        // Jeton inattendu : ignoré, comme GLPI qui ne l'ajoute pas à sa condition SQL.
        position++;
        return null;
    }

    /// <summary>Portage de Glpi\Api\HL\RSQL\Lexer::tokenize.</summary>
    private static List<(T Type, string Value)> Tokenize(string query)
    {
        List<(T, string)> tokens = [];
        int pos = 0;
        int length = query.Length;
        bool inFilter = false;
        StringBuilder buffer = new();

        static bool IsPropertyChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '.';

        while (pos < length)
        {
            char c = query[pos];
            char? prev = pos > 0 ? query[pos - 1] : null;

            if (c == '(' && prev != '\\')
            {
                tokens.Add((T.Open, "("));
            }
            else if (c == ')' && prev != '\\')
            {
                tokens.Add((T.Close, ")"));
            }
            else if (!inFilter && c == ';')
            {
                tokens.Add((T.And, ";"));
            }
            else if (!inFilter && c == ',')
            {
                tokens.Add((T.Or, ","));
            }
            else if (!inFilter && IsPropertyChar(c))
            {
                inFilter = true;
                buffer.Clear().Append(c);
                while (pos + 1 < length && query[pos + 1] != '=' && query[pos + 1] != '!')
                {
                    buffer.Append(query[++pos]);
                }
                string property = buffer.ToString();
                tokens.Add((T.Property, property));
                pos++;
                if (pos >= length || (query[pos] != '=' && query[pos] != '!'))
                {
                    throw new RsqlException($"RSQL query is missing an operator in filter for property \"{property}\"");
                }
                buffer.Clear().Append(query[pos]);
                while (pos + 1 < length && query[pos + 1] != '=')
                {
                    buffer.Append(query[++pos]);
                }
                if (pos + 1 >= length || query[pos + 1] != '=')
                {
                    throw new RsqlException($"RSQL query has an incomplete operator in filter for property \"{property}\"");
                }
                tokens.Add((T.Operator, buffer.Append('=').ToString()));
                buffer.Clear();
                pos += 2;

                char current = pos < length ? query[pos] : '\0';
                if (pos >= length || current == ';' || current == ',')
                {
                    tokens.Add((T.Unspecified, string.Empty));
                    inFilter = false;
                    if (pos >= length)
                    {
                        break;
                    }
                    continue;
                }

                char? expectedEnd = current switch { '(' => ')', '"' => '"', '\'' => '\'', _ => null };
                buffer.Append(current);
                bool closed = false;
                while (pos + 1 < length)
                {
                    char ch = query[++pos];
                    if (ch == '\\')
                    {
                        buffer.Append(ch);
                    }
                    else if (expectedEnd is not null && ch == expectedEnd)
                    {
                        buffer.Append(ch);
                        tokens.Add((T.Value, buffer.ToString()));
                        buffer.Clear();
                        closed = true;
                        break;
                    }
                    else if (expectedEnd is null && ch is ';' or ',' or ')')
                    {
                        tokens.Add((T.Value, buffer.ToString()));
                        buffer.Clear();
                        closed = true;
                        pos--;
                        break;
                    }
                    else
                    {
                        buffer.Append(ch);
                    }
                }
                inFilter = false;
                if (!closed)
                {
                    if (buffer.Length > 0)
                    {
                        tokens.Add((T.Value, buffer.ToString()));
                        buffer.Clear();
                    }
                    else
                    {
                        tokens.Add((T.Unspecified, string.Empty));
                    }
                }
            }
            pos++;
        }

        int opens = tokens.Count(t => t.Item1 == T.Open);
        int closes = tokens.Count(t => t.Item1 == T.Close);
        if (opens != closes)
        {
            throw new RsqlException("RSQL query has one or more unclosed groups");
        }
        return tokens;
    }
}
