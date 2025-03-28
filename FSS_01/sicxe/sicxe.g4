grammar sicxe;

prog            :   inicio proposiciones fin EOF;

inicio          :   etiqueta START NUM FINL
                ;

START           :   'START';

fin             :   END ID?
                ;

END			    :   'END';

proposiciones   :   (proposicion FINL)+
                ;

proposicion     :   directiva 
                |   instruccion
                ;


directiva       :   etiqueta? (RESB | RESW) NUM
                |   etiqueta? WORD (EXPR | NUM)
                |   etiqueta? EQU (EXPR | CPREF)
                |   etiqueta? BASE ID
                |   etiqueta? BYTE (CONSTHEX | CONSTCAD)
                |   USE ID?
                ;

instruccion     :   etiqueta opinstruccion
                |   opinstruccion
                ;

RESB            :   'RESB';
RESW            :   'RESW';
WORD            :   'WORD';
BYTE            :   'BYTE';
EQU             :   'EQU';
BASE            :   'BASE';
CPREF           :   '*';
USE			    :   'USE'; 

etiqueta        :   ID;

opinstruccion   :   formato;

formato         :   f4 | f3 | f2 | f1 ;

f4              :   CODOPF4 (simple | indirecto | inmediato)
                |   PLUS RSUB
                ;

f3              :   CODOPF3 (simple | indirecto | inmediato)
                |   RSUB
                ;

RSUB            :   'RSUB';

simple          :   (NUM | ID | EXPR) (',' REG)?;

indirecto       :   SIMIND (EXPR | NUM | ID);

SIMIND          :   '@';

inmediato       :   SIMINM (EXPR | NUM | ID);

SIMINM          :   '#';

f2              :   CODOPF2 (REG ',' (REG | NUM)) 
                |   CODOPF2 REG
                |   CODOPF2 NUM
                ;

f1              :   CODOPF1;

CODOPF1         :   'FIX' | 'FLOAT' | 'HIO' | 'NORM' | 'SIO' | 'TIO';

CODOPF2         :   'ADDR' | 'CLEAR' | 'COMPR' | 'DIVR' | 'MULR' | 'RMO' 
                |   'SHIFTL' | 'SHIFTR' | 'SVC' | 'TIXR' | 'SUBR' 
                ;
                
CODOPF4         :   PLUS CODOPF3
                ;

CODOPF3         :   'ADD' | 'ADDF' | 'AND' | 'COMP' | 'COMPF' | 'DIV' 
                |   'DIVF' | 'J' | 'JEQ' | 'JGT' | 'JLT' | 'JSUB' | 'LDA' 
                |   'LDB' | 'LDCH' | 'LDF' | 'LDL' | 'LDS' | 'LDT' | 'LDX' 
                |   'LPS' | 'MUL' | 'MULF' | 'OR' | 'RD' | 'RSUB' | 'SSK' 
                |   'STA' | 'STB' | 'STCH' | 'STF' | 'STI' | 'STL' | 'STS' 
                |   'STSW' | 'STT' | 'STX' | 'SUB' | 'SUBF' | 'SVC' | 'TD' 
                |   'TIX' | 'WD'
                ;


REG             : 'A' | 'X' | 'L' | 'B' | 'S' | 'T' | 'F' | 'SW'; 

NUM             : [0-9]+ ('H' | 'h')?;


CONSTHEX        : 'X' APOS [0-9A-F]+ APOS;

CONSTCAD        : 'C' APOS [a-zA-Z0-9]* APOS;

APOS            : '\'';   


ID              : [a-zA-Z_][a-zA-Z_0-9]*;

EXPR            : TERM EXPR2
                ;

EXPR2           : ('+' TERM | '-' TERM) EXPR2?
                | 
                ; 

TERM            : FACTOR TERM2
                ;

TERM2           : ('*' FACTOR | '/' FACTOR) TERM2?
                | 
                ; 

FACTOR          : NUM
                | ID 
                | '(' TERM ')'
                ;

PLUS			: '+';  

FINL            : ('\r'? '\n')+;


WS              : [ \t]+ -> skip;