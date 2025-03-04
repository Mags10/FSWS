using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSS_01
{
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
