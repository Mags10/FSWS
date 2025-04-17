PRINCIPAL   START   0    
            EXTDEF  LISTA    
            EXTREF  LISTB, ENDB   
            EXTDEF  ENDA    
REF1        +LDA    LISTB+6   
REF2        LDA     #(ENDA-LISTA+5)  
LISTA       EQU     *    
REF3        WORD    ENDA-(ENDB-LISTB)+4  
ENDA        EQU     *    
MODULO      CSECT      
            EXTDEF  BUFFER, INPUT  
            EXTREF  ENDA    
FIRST       STL     RETADR   
            CLEAR   A    
CLOOP       JSUB    RDREC    
            LDA     LENGTH   
            USE     DATOS    
RETADR      RESW    2    
LENGTH      RESW    3    
            USE     BLOCK    
BUFFER      RESB    4096    
            WORD    ENDA+6   
BUFFEND     EQU     *    
MAXLEN      EQU     BUFFEND-BUFFER+3   
            WORD    BUFFEND+6   
            USE      
RDREC       CLEAR   X    
            +LDT    #MAXLEN   
            USE     DATOS    
INPUT       BYTE    X'0F1'    
            USE      
            +LDT    ENDA    
            END     REF2   