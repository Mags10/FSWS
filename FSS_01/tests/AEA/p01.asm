PROG    START   0                      	
EXTREF  NUM1, TABLE2, PROG1           
EXTDEF  COUNT, TABLE           	
FIRST   LDX     #0            
        +LDB    #TABLE2           
        BASE    TABLE           
        +TIX    NUM1           	
        +STA    PROG1, X          	
        ADD     TABLE, X           
COUNT   RESW    1                   
TABLE   RESB    4096            
TOTAL   RESW    1            
        PROG1   CSECT           	
EXTDEF  NUM1, TABLE2          	
EXTREF  COUNT, TABLE          	
        LDB     #TABLE2          
NUM1    RESW    1                  	
TABLE2  RESB    4096          	
TOTAL   WORD    COUNT-TABLE+NUM1       
        END     FIRST 