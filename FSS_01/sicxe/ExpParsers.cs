using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace FSS_01.sicxe
{

    #region Lexer y Tokens

    public enum TokenType
    {
        Number,
        Identifier,
        Plus,
        Minus,
        Multiply,
        Divide,
        LParen,
        RParen,
        End
    }

    public class Token
    {
        public TokenType Type;
        public string Value;
        public Token(TokenType type, string value) { Type = type; Value = value; }
    }

    public class Lexer
    {
        string text;
        int pos;
        public Lexer(string text) { this.text = text; pos = 0; }
        public Token GetNextToken()
        {
            while (pos < text.Length && Char.IsWhiteSpace(text[pos]))
                pos++;
            if (pos >= text.Length)
                return new Token(TokenType.End, "");
            char current = text[pos];
            if (Char.IsDigit(current))
            {
                int start = pos;
                while (pos < text.Length && Char.IsDigit(text[pos]))
                    pos++;
                // El valor numérico será reemplazado por "ABS"
                return new Token(TokenType.Number, text.Substring(start, pos - start));
            }
            if (Char.IsLetter(current))
            {
                int start = pos;
                while (pos < text.Length && Char.IsLetter(text[pos]))
                    pos++;
                string ident = text.Substring(start, pos - start);
                return new Token(TokenType.Identifier, ident);
            }
            pos++;
            switch (current)
            {
                case '+': return new Token(TokenType.Plus, "+");
                case '-': return new Token(TokenType.Minus, "-");
                case '*': return new Token(TokenType.Multiply, "*");
                case '/': return new Token(TokenType.Divide, "/");
                case '(': return new Token(TokenType.LParen, "(");
                case ')': return new Token(TokenType.RParen, ")");
                default:
                    throw new Exception("Caracter inesperado: " + current);
            }
        }
    }

    #endregion

    #region AST y Parser

    public abstract class AstNode { }

    public class NumberNode : AstNode
    {
        public string Value;
        public NumberNode(string value) { Value = value; }
    }

    public class IdentifierNode : AstNode
    {
        public string Name;
        public IdentifierNode(string name) { Name = name; }
    }

    public class UnaryNode : AstNode
    {
        public string Op; // "+" o "-"
        public AstNode Expr;
        public UnaryNode(string op, AstNode expr) { Op = op; Expr = expr; }
    }

    public class BinaryNode : AstNode
    {
        public AstNode Left;
        public string Op; // "+", "-", "*" o "/"
        public AstNode Right;
        public BinaryNode(AstNode left, string op, AstNode right) { Left = left; Op = op; Right = right; }
    }

    public class ParenthesizedNode : AstNode
    {
        public AstNode Inner;
        public ParenthesizedNode(AstNode inner) { Inner = inner; }
    }

    public class Parser
    {
        Lexer lexer;
        Token currentToken;
        public Parser(Lexer lexer)
        {
            this.lexer = lexer;
            currentToken = lexer.GetNextToken();
        }
        void Eat(TokenType type)
        {
            if (currentToken.Type == type)
                currentToken = lexer.GetNextToken();
            else
                throw new Exception("Token inesperado: " + currentToken.Value);
        }
        // Factor: Number | Identifier | '(' Expr ')' | ('+'|'-') Factor
        public AstNode Factor()
        {
            Token token = currentToken;
            if (token.Type == TokenType.Plus)
            {
                Eat(TokenType.Plus);
                return new UnaryNode("+", Factor());
            }
            if (token.Type == TokenType.Minus)
            {
                Eat(TokenType.Minus);
                return new UnaryNode("-", Factor());
            }
            if (token.Type == TokenType.Number)
            {
                Eat(TokenType.Number);
                return new NumberNode(token.Value);
            }
            if (token.Type == TokenType.Identifier)
            {
                Eat(TokenType.Identifier);
                return new IdentifierNode(token.Value);
            }
            if (token.Type == TokenType.LParen)
            {
                Eat(TokenType.LParen);
                AstNode node = Expr();
                Eat(TokenType.RParen);
                return new ParenthesizedNode(node);
            }
            throw new Exception("Token inesperado en Factor: " + token.Value);
        }
        // Term: Factor (( '*' | '/') Factor)*
        public AstNode Term()
        {
            AstNode node = Factor();
            while (currentToken.Type == TokenType.Multiply || currentToken.Type == TokenType.Divide)
            {
                Token token = currentToken;
                if (token.Type == TokenType.Multiply)
                {
                    Eat(TokenType.Multiply);
                    node = new BinaryNode(node, "*", Factor());
                }
                else
                {
                    Eat(TokenType.Divide);
                    node = new BinaryNode(node, "/", Factor());
                }
            }
            return node;
        }
        // Expr: Term (( '+' | '-') Term)*
        public AstNode Expr()
        {
            AstNode node = Term();
            while (currentToken.Type == TokenType.Plus || currentToken.Type == TokenType.Minus)
            {
                Token token = currentToken;
                if (token.Type == TokenType.Plus)
                {
                    Eat(TokenType.Plus);
                    node = new BinaryNode(node, "+", Term());
                }
                else
                {
                    Eat(TokenType.Minus);
                    node = new BinaryNode(node, "-", Term());
                }
            }
            return node;
        }
    }

    #endregion

    #region ExpressionTransformer

    public class ExpressionTransformer
    {
        /// <summary>
        /// Transforma la expresión para que cada REL o ABS tenga un signo explícito,
        /// propagando los negativos de paréntesis completos.
        /// </summary>
        public static string Transform(string input)
        {
            // Paso 1: Reemplazar números por "ABS"
            string replaced = Regex.Replace(input, "(?<![A-Za-z])\\d+(?![A-Za-z])", "ABS");
            // Paso 2: Parsear la expresión
            Lexer lexer = new Lexer(replaced);
            Parser parser = new Parser(lexer);
            AstNode tree = parser.Expr();
            // Paso 3: Propagar los signos por todo el AST
            AstNode propagated = PropagateSigns(tree, 1);
            // Paso 4: Serializar el AST (se mostrará cada REL o ABS con signo)
            string result = Serialize(propagated);
            // Eliminar un posible signo '+' inicial redundante
            if (result.StartsWith("+"))
                result = result.Substring(1);

            // Remplazar +- por -
            result = result.Replace("+-", "-");
            result = result.Replace("-+", "-");
            result = result.Replace("--", "+");
            result = result.Replace("++", "+");
            return result;
        }

        /// <summary>
        /// Propaga el multiplicador de signo (1 o -1) en el AST.
        /// Para hojas, envuelve en un nodo unario con el signo explícito.
        /// Para BinaryNode, convierte A - B en A + (inversión de B).
        /// </summary>
        static AstNode PropagateSigns(AstNode node, int multiplier)
        {
            if (node is UnaryNode un)
            {
                // Si es unario, multiplica el signo
                if (un.Op == "+")
                    return PropagateSigns(un.Expr, multiplier);
                else // "-"
                    return PropagateSigns(un.Expr, multiplier * -1);
            }
            if (node is BinaryNode bin)
            {
                // Para '+' y '-' se propagan de forma diferente
                if (bin.Op == "+")
                {
                    AstNode left = PropagateSigns(bin.Left, multiplier);
                    AstNode right = PropagateSigns(bin.Right, multiplier);
                    return new BinaryNode(left, "+", right);
                }
                else if (bin.Op == "-")
                {
                    // A - B se convierte en A + (inversión de B)
                    AstNode left = PropagateSigns(bin.Left, multiplier);
                    AstNode right = PropagateSigns(bin.Right, multiplier * -1);
                    return new BinaryNode(left, "+", right);
                }
                else if (bin.Op == "*" || bin.Op == "/")
                {
                    // Para multiplicación o división, no se distribuye el signo internamente
                    // Se evalúa cada operando sin alterar su signo; luego, si el multiplicador global es -1, se envuelve la operación en un unario.
                    AstNode left = PropagateSigns(bin.Left, 1);
                    AstNode right = PropagateSigns(bin.Right, 1);
                    AstNode newBin = new BinaryNode(left, bin.Op, right);
                    return multiplier == 1 ? newBin : new UnaryNode(multiplier == 1 ? "+" : "-", newBin);
                }
            }
            if (node is ParenthesizedNode par)
            {
                AstNode inner = PropagateSigns(par.Inner, multiplier);
                return new ParenthesizedNode(inner);
            }
            // Hojas: NumberNode o IdentifierNode
            if (node is NumberNode || node is IdentifierNode)
            {
                // Envuelve la hoja en un unario con signo explícito
                string sign = multiplier == 1 ? "+" : "-";
                return new UnaryNode(sign, node);
            }
            return node;
        }

        /// <summary>
        /// Serializa el AST a una cadena.
        /// </summary>
        static string Serialize(AstNode node)
        {
            if (node is NumberNode)
                return "ABS";
            if (node is IdentifierNode id)
                return id.Name;
            if (node is UnaryNode un)
            {
                // Si el hijo es una hoja, no agregar paréntesis
                if (un.Expr is NumberNode || un.Expr is IdentifierNode)
                    return un.Op + Serialize(un.Expr);
                else
                    return un.Op + "(" + Serialize(un.Expr) + ")";
            }
            if (node is BinaryNode bin)
            {
                return Serialize(bin.Left) + bin.Op + Serialize(bin.Right);
            }
            if (node is ParenthesizedNode par)
            {
                return "(" + Serialize(par.Inner) + ")";
            }
            return "";
        }
    }

    #endregion

}