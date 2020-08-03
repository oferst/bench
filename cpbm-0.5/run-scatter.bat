rem requires two csv files
del filename.txt
rem since make_graph ordres the files internally alphabetically we have to do it here
rem as well
if "%1" leq "%2" (
set A=%1
set B=%2
) else (
set A=%2
set B=%1
)
make_graph.pl -s %A%.csv %B%.csv
del %A%_%B%-scatter.pdf
pdflatex %A%_%B%-scatter.tex -quiet
echo generated %A%_%B%-scatter.pdf
if exist %A%_%B%-scatter.pdf start %A%_%B%-scatter.pdf
