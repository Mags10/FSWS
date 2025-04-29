using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace FSS_01
{
    internal partial class CompiladorSx
    {

        public CompiladorSx()
        {
            // Cargar las instrucciones y sus códigos de operación para todas las instancias
            if (!inic)
            {
                var json = System.IO.File.ReadAllText("../../sicxe/sicxe.json");
                var jsonObject = JsonConvert.DeserializeObject<JObject>(json);
                var instrucciones = jsonObject["instrucciones"].ToObject<Dictionary<string, string>>();
                var registros = jsonObject["registros"].ToObject<Dictionary<string, string>>();
                foreach (var instr in instrucciones)
                    opers.Add(new Tuple<string, int>(instr.Key, int.Parse(instr.Value, System.Globalization.NumberStyles.HexNumber)));
                foreach (var reg in registros)
                    regs.Add(new Tuple<string, int>(reg.Key, int.Parse(reg.Value, System.Globalization.NumberStyles.HexNumber)));
                inic = true;
            }
        }


        public void setCode(string code)
        {
            // Crear un objeto de la clase AntlrInputStream
            input = new AntlrInputStream(code);
            // Crear un objeto de la clase AsmLexer
            lexer = new sicxeLexer(input);

            var lexr = this.lexer;
            // Crear lista de reglas
            ruleList.Clear();
            foreach (var rule in lexr.RuleNames)
                ruleList.Add(new Tuple<string, int>(rule, (int)lexr.GetType().GetField(rule).GetValue(lexr)));
            // Crear un objeto de la clase CommonTokenStream
            tokens = new CommonTokenStream(lexer);
            // Crear un objeto de la clase AsmParser
            parser = new sicxeParser(tokens);

            // Listener de errores
            parslistener = new ErrorParserListener();
            lexelistener = new ErrorLexerListener();
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexelistener);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(parslistener);

            // Reinciar lo necesario
            lineas.Clear();
            secciones.Clear();
            midFile = null;
            programObj = "";
            date = DateTime.Now;
            alreadyCompiled = false;
            step = 0;

            tree = parser.prog();
        }

    }
}
