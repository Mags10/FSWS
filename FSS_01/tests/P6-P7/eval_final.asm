EVAL_FINAL	START	0
			SHIFTL	S, 10
TABLA		RESW	12
VALOR		EQU		*
CLOOP		STL		TEXT, X
			LDA		110H, X
			USE		DATOS
NUM			EQU		14
TEXT		BYTE	X'034'
CTE			WORD	125
			ORG		64H
CAD			BYTE	C'NOMBRE'
CONT		EQU		*
EXPR		EQU		NUM+TABLA
EXPRE2		WORD	(NUM+TABLA-CTE)*10
			USE
			+COMP	#CTE+3
			LDS		TEX, X
			CLEAR	T
			USE		RUTINA
			WORD	(CAD+VALOR)-NUM
SALTO		MUL		CTE, X
			+LDT	#NUM-SALTO
			J		SALTO, X
CLOOP		+STA	-SALTO+NUM-(-VALOR-CONT), X
			USE		DATOS
			WORD	((SALTO-CTE)-(-EXPRE2))+15
			COMP	#NUM
CALC		EQU		((SALTO-CTE)-(-EXPRE2))+15
			+LDA 	NUM, X
			END		FIRST