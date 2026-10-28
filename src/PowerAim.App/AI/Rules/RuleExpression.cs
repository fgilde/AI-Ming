using System.Globalization;

namespace PowerAim.AI.Rules;

/// <summary>A value inside a rule: a number, or text (only for <c>class == "head"</c>-style comparisons).</summary>
public readonly record struct RuleValue(double Number, string? Text = null)
{
    public static readonly RuleValue Missing = new(double.NaN);
    public static RuleValue Of(bool b) => new(b ? 1 : 0);
    public bool IsText => Text != null;
    public bool IsTrue => !IsText && !double.IsNaN(Number) && Number != 0;
}

/// <summary>Supplies signal values by name while a rule is evaluated.</summary>
public interface IRuleScope
{
    RuleValue Get(string name);
}

public sealed class RuleSyntaxException(string message, int position) : Exception(message)
{
    public int Position { get; } = position;
}

/// <summary>
///     A small condition/score language for triggers and aim, e.g.
///     <c>head_hit &gt;= 0.8 and distance &lt; 120</c> or <c>-distance + 200 * confidence</c>.
///     Numbers (<c>80%</c> = 0.8), <c>"text"</c>, signals, <c>+ - * /</c>, comparisons
///     (<c>&lt; &lt;= &gt; &gt;= == != =</c>), <c>and or not</c> (also <c>&amp;&amp; || !</c>), parentheses and
///     <c>min max abs clamp</c>. Comparisons yield 1/0; a missing signal is NaN and makes comparisons false.
///     Parsed once, evaluated per target per frame (a few hundred nanoseconds).
/// </summary>
public sealed class RuleExpression
{
    private readonly Node _root;

    public string Source { get; }

    /// <summary>Every signal name the rule reads.</summary>
    public IReadOnlyCollection<string> Signals { get; }

    private RuleExpression(string source, Node root, IReadOnlyCollection<string> signals)
    {
        Source = source;
        _root = root;
        Signals = signals;
    }

    /// <summary>Parses <paramref name="text"/>; <paramref name="isKnownSignal"/> rejects typos at parse time.</summary>
    public static RuleExpression Parse(string text, Func<string, bool>? isKnownSignal = null)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new RuleSyntaxException("empty rule", 0);
        var parser = new Parser(Tokenize(text));
        var root = parser.ParseExpression();
        parser.ExpectEnd();
        foreach (var (name, pos) in parser.Variables)
            if (isKnownSignal != null && !isKnownSignal(name))
                throw new RuleSyntaxException($"unknown signal '{name}'", pos);
        return new RuleExpression(text.Trim(), root, parser.Variables.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public static bool TryParse(string text, Func<string, bool>? isKnownSignal, out RuleExpression? rule, out string? error)
    {
        try { rule = Parse(text, isKnownSignal); error = null; return true; }
        catch (RuleSyntaxException e) { rule = null; error = $"{e.Message} (at {e.Position + 1})"; return false; }
    }

    public RuleValue Evaluate(IRuleScope scope) => _root.Eval(scope);

    public double Score(IRuleScope scope) => Evaluate(scope) is { IsText: false } v ? v.Number : double.NaN;

    public bool IsTrue(IRuleScope scope) => Evaluate(scope).IsTrue;

    public override string ToString() => Source;

    // ------------------------------------------------------------------ tokens

    private enum Tok { Num, Str, Ident, Op, LParen, RParen, Comma, End }

    private readonly record struct Token(Tok Kind, string Text, double Number, int Pos);

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            int start = i;
            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                if (!double.TryParse(s[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    throw new RuleSyntaxException($"bad number '{s[start..i]}'", start);
                if (i < s.Length && s[i] == '%') { n /= 100; i++; }
                tokens.Add(new Token(Tok.Num, s[start..i], n, start));
            }
            else if (c is '"' or '\'')
            {
                int end = s.IndexOf(c, i + 1);
                if (end < 0) throw new RuleSyntaxException("unterminated text", start);
                tokens.Add(new Token(Tok.Str, s[(i + 1)..end], 0, start));
                i = end + 1;
            }
            else if (char.IsLetter(c) || c == '_')
            {
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '_' or '.')) i++;
                var word = s[start..i];
                switch (word.ToLowerInvariant())
                {
                    case "and": tokens.Add(new Token(Tok.Op, "&&", 0, start)); break;
                    case "or": tokens.Add(new Token(Tok.Op, "||", 0, start)); break;
                    case "not": tokens.Add(new Token(Tok.Op, "!", 0, start)); break;
                    case "true": tokens.Add(new Token(Tok.Num, word, 1, start)); break;
                    case "false": tokens.Add(new Token(Tok.Num, word, 0, start)); break;
                    default: tokens.Add(new Token(Tok.Ident, word, 0, start)); break;
                }
            }
            else if (c == '(') { tokens.Add(new Token(Tok.LParen, "(", 0, i++)); }
            else if (c == ')') { tokens.Add(new Token(Tok.RParen, ")", 0, i++)); }
            else if (c == ',') { tokens.Add(new Token(Tok.Comma, ",", 0, i++)); }
            else
            {
                string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                if (two is "<=" or ">=" or "==" or "!=" or "&&" or "||") { tokens.Add(new Token(Tok.Op, two, 0, i)); i += 2; }
                else if (c is '<' or '>' or '+' or '-' or '*' or '/' or '!') { tokens.Add(new Token(Tok.Op, c.ToString(), 0, i++)); }
                else if (c == '=') { tokens.Add(new Token(Tok.Op, "==", 0, i++)); }
                else throw new RuleSyntaxException($"unexpected '{c}'", i);
            }
        }
        tokens.Add(new Token(Tok.End, "", 0, s.Length));
        return tokens;
    }

    // ------------------------------------------------------------------ parser (precedence climbing)

    private sealed class Parser(List<Token> tokens)
    {
        private int _i;
        public List<(string Name, int Pos)> Variables { get; } = new();

        private Token Peek => tokens[_i];
        private Token Next() => tokens[_i++];
        private bool IsOp(string op) => Peek.Kind == Tok.Op && Peek.Text == op;

        public void ExpectEnd()
        {
            if (Peek.Kind != Tok.End) throw new RuleSyntaxException($"unexpected '{Peek.Text}'", Peek.Pos);
        }

        public Node ParseExpression() => Or();

        private Node Or()
        {
            var left = And();
            while (IsOp("||")) { Next(); left = new Binary("||", left, And()); }
            return left;
        }

        private Node And()
        {
            var left = Not();
            while (IsOp("&&")) { Next(); left = new Binary("&&", left, Not()); }
            return left;
        }

        private Node Not()
        {
            if (IsOp("!")) { Next(); return new Unary("!", Not()); }
            return Compare();
        }

        private Node Compare()
        {
            var left = Sum();
            if (Peek.Kind == Tok.Op && Peek.Text is "<" or "<=" or ">" or ">=" or "==" or "!=")
                return new Binary(Next().Text, left, Sum());
            return left;
        }

        private Node Sum()
        {
            var left = Product();
            while (IsOp("+") || IsOp("-")) left = new Binary(Next().Text, left, Product());
            return left;
        }

        private Node Product()
        {
            var left = UnaryMinus();
            while (IsOp("*") || IsOp("/")) left = new Binary(Next().Text, left, UnaryMinus());
            return left;
        }

        private Node UnaryMinus()
        {
            if (IsOp("-")) { Next(); return new Unary("-", UnaryMinus()); }
            return Primary();
        }

        private Node Primary()
        {
            var t = Next();
            switch (t.Kind)
            {
                case Tok.Num: return new Constant(new RuleValue(t.Number));
                case Tok.Str: return new Constant(new RuleValue(double.NaN, t.Text));
                case Tok.LParen:
                {
                    var inner = ParseExpression();
                    if (Next().Kind != Tok.RParen) throw new RuleSyntaxException("missing ')'", t.Pos);
                    return inner;
                }
                case Tok.Ident when Peek.Kind == Tok.LParen:
                {
                    Next();
                    var args = new List<Node>();
                    if (Peek.Kind != Tok.RParen)
                    {
                        args.Add(ParseExpression());
                        while (Peek.Kind == Tok.Comma) { Next(); args.Add(ParseExpression()); }
                    }
                    if (Next().Kind != Tok.RParen) throw new RuleSyntaxException("missing ')'", t.Pos);
                    return Call.Create(t.Text.ToLowerInvariant(), args, t.Pos);
                }
                case Tok.Ident:
                    Variables.Add((t.Text, t.Pos));
                    return new Variable(t.Text);
                default:
                    throw new RuleSyntaxException(t.Kind == Tok.End ? "rule ends too early" : $"unexpected '{t.Text}'", t.Pos);
            }
        }
    }

    // ------------------------------------------------------------------ nodes

    private abstract class Node
    {
        public abstract RuleValue Eval(IRuleScope scope);
    }

    private sealed class Constant(RuleValue value) : Node
    {
        public override RuleValue Eval(IRuleScope scope) => value;
    }

    private sealed class Variable(string name) : Node
    {
        public override RuleValue Eval(IRuleScope scope) => scope.Get(name);
    }

    private sealed class Unary(string op, Node operand) : Node
    {
        public override RuleValue Eval(IRuleScope scope)
        {
            var v = operand.Eval(scope);
            return op == "!" ? RuleValue.Of(!v.IsTrue) : new RuleValue(v.IsText ? double.NaN : -v.Number);
        }
    }

    private sealed class Binary(string op, Node left, Node right) : Node
    {
        public override RuleValue Eval(IRuleScope scope)
        {
            if (op == "&&") return RuleValue.Of(left.Eval(scope).IsTrue && right.Eval(scope).IsTrue);
            if (op == "||") return RuleValue.Of(left.Eval(scope).IsTrue || right.Eval(scope).IsTrue);

            var a = left.Eval(scope);
            var b = right.Eval(scope);
            if (a.IsText || b.IsText)
            {
                bool same = a.IsText && b.IsText && string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
                return op switch { "==" => RuleValue.Of(same), "!=" => RuleValue.Of(a.IsText && b.IsText && !same), _ => RuleValue.Missing };
            }
            double x = a.Number, y = b.Number;
            return op switch
            {
                "+" => new RuleValue(x + y),
                "-" => new RuleValue(x - y),
                "*" => new RuleValue(x * y),
                "/" => new RuleValue(y == 0 ? double.NaN : x / y),
                "<" => RuleValue.Of(x < y),
                "<=" => RuleValue.Of(x <= y),
                ">" => RuleValue.Of(x > y),
                ">=" => RuleValue.Of(x >= y),
                "==" => RuleValue.Of(Math.Abs(x - y) < 1e-9),
                "!=" => RuleValue.Of(!double.IsNaN(x) && !double.IsNaN(y) && Math.Abs(x - y) >= 1e-9),
                _ => RuleValue.Missing,
            };
        }
    }

    private sealed class Call(Func<double[], double> fn, Node[] args) : Node
    {
        public static Node Create(string name, List<Node> args, int pos)
        {
            (int Arity, Func<double[], double> Fn) def = name switch
            {
                "min" => (2, a => Math.Min(a[0], a[1])),
                "max" => (2, a => Math.Max(a[0], a[1])),
                "abs" => (1, a => Math.Abs(a[0])),
                "clamp" => (3, a => Math.Clamp(a[0], Math.Min(a[1], a[2]), Math.Max(a[1], a[2]))),
                _ => throw new RuleSyntaxException($"unknown function '{name}'", pos),
            };
            if (args.Count != def.Arity) throw new RuleSyntaxException($"{name} takes {def.Arity} values", pos);
            return new Call(def.Fn, args.ToArray());
        }

        public override RuleValue Eval(IRuleScope scope)
        {
            var values = new double[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var v = args[i].Eval(scope);
                values[i] = v.IsText ? double.NaN : v.Number;
            }
            return new RuleValue(fn(values));
        }
    }
}
