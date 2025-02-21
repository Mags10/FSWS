grammar sicxe;

prog            :   inicio proposiciones fin EOF;

inicio          :   etiqueta START NUM FINL
                ;

fin             :   END entrada
                ;

entrada         :   ID?
                ;

proposiciones   :   (proposicion FINL)+
                ;

proposicion     :   directiva 
                |   instruccion
                ;

instruccion     :   (etiqueta opinstruccion) 
                |   opinstruccion
                ;

directiva       :   etiqueta? tipodirectiva opdirectiva
                |   etiqueta? BASE ID
                ;

tipodirectiva   :   BYTE | WORD | RESB | RESW;

etiqueta        :   ID;

opinstruccion   :   formato;

formato         :   f4 | f3 | f2 | f1 ;

f4              :   '+' f3;

f3              :   simple3 | indirecto3 | inmediato3 | 'RSUB';

f2              :   CODOPF2 (REG ',' (REG | NUM)) 
                |   CODOPF2 REG
                |   CODOPF2 NUM
                ;

f1              :   CODOPF1;

simple3         :   CODOPF3 (NUM | ID) (',' REG)?;

indirecto3      :   CODOPF3 '@' (NUM | ID);

inmediato3      :   CODOPF3 '#' (NUM | ID);

opdirectiva     :   NUM | CONSTHEX | CONSTCAD;

END             :   'END';

BASE            :   'BASE';

BYTE            :   'BYTE';

WORD            :   'WORD';

RESB            :   'RESB';

RESW            :   'RESW';

START           :   'START';

CODOPF1         :   'FIX' | 'FLOAT' | 'HIO' | 'NORM' | 'SIO' | 'TIO';

CODOPF2         :   'ADDR' | 'CLEAR' | 'COMPR' | 'DIVR' | 'MULR' | 'RMO' 
                |   'SHIFTL' | 'SHIFTR' | 'SVC' | 'TIXR' | 'SUBR' 
                ;

CODOPF3         :   'ADD' | 'ADDF' | 'AND' | 'COMP' | 'COMPF' | 'DIV' 
                |   'DIVF' | 'J' | 'JEQ' | 'JGT' | 'JLT' | 'JSUB' | 'LDA' 
                |   'LDB' | 'LDCH' | 'LDF' | 'LDL' | 'LDS' | 'LDT' | 'LDX' 
                |   'LPS' | 'MUL' | 'MULF' | 'OR' | 'RD' | 'RSUB' | 'SSK' 
                |   'STA' | 'STB' | 'STCH' | 'STF' | 'STI' | 'STL' | 'STS' 
                |   'STSW' | 'STT' | 'STX' | 'SUB' | 'SUBF' | 'SVC' | 'TD' 
                |   'TIX' | 'WD'
                ;

REG             : 'A' | 'X' | 'L' | 'B' | 'S' | 'T' | 'F' | 'SW';  // Define todos los registros aquís

NUM             : [0-9]+ ('H' | 'h')?;

ID              : [a-zA-Z_][a-zA-Z_0-9]*;

FINL            : ('\r'? '\n')+;

CONSTHEX        : 'X''\'' [0-9A-F]+ '\'';

CONSTCAD        : 'C''\'' [a-zA-Z0-9]* '\'';

WS              : [ \t]+ -> skip; // Evitar problemas con saltos de línea