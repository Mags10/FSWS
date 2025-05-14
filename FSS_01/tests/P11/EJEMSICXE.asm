EJEMSICXE  START   0H
           EXTDEF  TAM, SALTO
           EXTREF  SIMBOLO
           EXTDEF  COUNT
TABLA      RESW    3
NUM        EQU     19
           BASE    SALTO
ETIQ       CLEAR   X
MAX        EQU     SALTO+2
TAM        EQU     *
           +TIX    SIMBOLO
           LDA     #(TABLA-ETIQ+3)
           ORG     3060H
SALTO      JLT     ETIQ
           LDT     COUNT+4
COUNT      WORD    ETIQ-(TAM-TABLA)
           WORD    2*(SALTO-TAM)
           RESB    86
           WORD    4*(SALTO-TAMANO)
           END     SALTO
