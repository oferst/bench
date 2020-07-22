echo off
rem each will be a line in the cactus
make_graph.pl -c %1 %2 %3 %4 %5 %6 %7 %8 %9
rem in the following, filename.txt is written to inside make_graph.pl
for /f "tokens=*" %%a in (filename.txt) do (   
del %%a.pdf
pdflatex %%a.tex -quiet
if exist %%a.pdf start %%a.pdf
)
