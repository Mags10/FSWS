EJER1	START	0
		LDX		#5
		HIO
		+LDB	#TABLE2
		STA		@COUNT
		CLEAR	A
		BASE	TABLE2
		ADDR	X, A
LOOP	ADD		TABLE, X
		+STA	TOTAL
		RSUB
COUNT	RESB	12H
		SHIFTL	X, 2
TABLE	RESW	10
TABLE2	BYTE	C'TEST'
		WORD	16
		END