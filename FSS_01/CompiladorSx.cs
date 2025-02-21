using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSS_01
{
    internal class CompiladorSx
    {
        public string asm_code;
        public AntlrInputStream input;
        public sicxeLexer lexer;
        public CommonTokenStream tokens;
        public sicxeParser parser;
        public ErrorParserListener parslistener;
        public ErrorLexerListener lexelistener;
        public sicxeParser.ProgContext tree;
        private DateTime date;
        private bool alreadyCompiled = false;
        public List<Tuple<string, int>> ruleList = new List<Tuple<string, int>>();

        public CompiladorSx(string path)
        {
            loadCode(path);
        }

        public void loadCode(string path)
        {
            string[] lines = System.IO.File.ReadAllLines(path);
            // juntar todas las lineas en un solo string sin eliminar los saltos de linea
            string cad = string.Join("\n", lines);
            setCode(cad);
        }

        public void setCode(string code)
        {
            asm_code = code;
            // Crear un objeto de la clase AntlrInputStream
            input = new AntlrInputStream(code);
            // Crear un objeto de la clase AsmLexer
            lexer = new sicxeLexer(input);

            var lexr = this.lexer;
            // Crear lista de reglas
            ruleList.Clear();
            foreach (var rule in lexr.RuleNames)
            {
                ruleList.Add(new Tuple<string, int>(rule, (int)lexr.GetType().GetField(rule).GetValue(lexr)));
            }

            // Crear un objeto de la clase CommonTokenStream
            tokens = new CommonTokenStream(lexer);
            // Crear un objeto de la clase AsmParser
            parser = new sicxeParser(tokens);
            // Agregar los oyentes de errores
            parslistener = new ErrorParserListener();
            lexelistener = new ErrorLexerListener();
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexelistener);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(parslistener);
            alreadyCompiled = false;
        }

        public void compile()
        {
            tree = parser.prog();
            alreadyCompiled = true;
            date = DateTime.Now;
        }

        public string results()
        {
            if (!alreadyCompiled) return "El código no ha sido compilado";
            string output = "";
            // Obtener fecha y hora actual
            output += "Fecha y hora de compilación: " + date.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n";
            if (parslistener.getErroresCount() > 0)
            {
                output += "Errores sintácticos en el código: " + parslistener.getErroresCount() + "\n";
                output += parslistener.getErrores() + "\n";
            }
            else
            {
                output += "No se encontraron errores sintácticos en el código\n\n";
            }
            if (lexelistener.getErroresCount() > 0)
            {
                output += "Errores léxicos en el código: " + lexelistener.getErroresCount() + "\n";
                output += lexelistener.getErrores() + "\n";
            }
            else
            {
                output += "No se encontraron errores léxicos en el código\n\n";
            }

            output += "Árbol de análisis sintáctico:\n";
            output += tree.ToStringTree(parser) + "\n";
            return output;
        }

        public string getTokenType(int type)
        {
            foreach (var rule in ruleList)
            {
                if (rule.Item2 == type)
                {
                    return rule.Item1;
                }
            }
            return null;
        }

    }

    // Implementación de un oyente de errores del parser
    internal class ErrorParserListener : IAntlrErrorListener<IToken>
    {
        private String errores = "";
        public List<Tuple<int, string>> errorsLines = new List<Tuple<int, string>>();
        private int c = 0;

        public String getErrores()
        {
            return errores;
        }

        public int getErroresCount()
        {
            return c;
        }

        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            // Manejar el error según tus necesidades
            //Console.WriteLine($"Línea {line}:{charPositionInLine} - {msg}");
            errores += $"Line {line}:{charPositionInLine} - {msg}\n";
            errorsLines.Add(new Tuple<int, string>(line, msg));
            c++;
        }

        public string getErrorByLine(int line)
        {
            foreach (var error in errorsLines)
            {
                if (error.Item1 == line)
                {
                    return error.Item2;
                }
            }
            return null;
        }
    }

    // Implementación de un oyente de errores del lexer
    internal class ErrorLexerListener : IAntlrErrorListener<int>
    {
        private int c = 0;
        private String errores = "";
        public List<Tuple<int, string>> errorsLines = new List<Tuple<int, string>>();

        public String getErrores()
        {
            return errores;
        }

        public int getErroresCount()
        {
            return c;
        }

        public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            // Manejar el error según tus necesidades
            //Console.WriteLine($"Línea {line}:{charPositionInLine} - {msg}");
            errores += $"Line {line}:{charPositionInLine} - {msg}\n";
            errorsLines.Add(new Tuple<int, string>(line, msg));
            c++;
        }

        public string getErrorByLine(int line)
        {
            foreach (var error in errorsLines)
            {
                if (error.Item1 == line)
                {
                    return error.Item2;
                }
            }
            return null;
        }
    }
}
