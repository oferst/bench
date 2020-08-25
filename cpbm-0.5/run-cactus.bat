echo off
rem each param will be a line in the cactus
del filename.txt
SET ONE=%~1
SET TWO=%~2
SET THREE=%~3
SET FOUR=%~4
SET FIVE=%~5
SET SIX=%~6
SET SEVEN=%~7
SET EIGHT=%~8
SET NINE=%~9
SHIFT
SHIFT
SHIFT
SHIFT
SHIFT
SHIFT
SHIFT
SHIFT
SHIFT
SET TEN=%~1
SET ELEVEN=%~2
SET TWELVE=%~3
SET THIRTEEN=%~4
SET FOURTEEN=%~5
SET FIFTEEN=%~6
SET SIXTEEN=%~7
SET SEVENTEEN=%~8
SET EIGHTEEN=%~9
SHIFT
SHIFT
SET NONTEEN=%~8
SET TWENTY=%~9

make_graph.pl -c %ONE% %TWO% %THREE% %FOUR% %FIVE% %SIX% %SEVEN% %EIGHT% %NINE% %TEN% %ELEVEN% %TWELVE% %THIRTEEN% %FOURTEEN% %FIFTEEN% %SIXTEEN% %SEVENTEEN% %EIGHTEEN% %NONTEEN% %TWENTY% 


rem in the following, filename.txt is written to inside make_graph.pl
for /f "tokens=*" %%a in (filename.txt) do (   
del %%a.pdf
pdflatex %%a.tex -quiet
if exist %%a.pdf start %%a.pdf
)
