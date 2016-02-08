echo running %1, %2, %3...
rem ssh ofers@tamnun.technion.ac.il "rm Remote_hmuc.log"  
scp %1 ofers@tamnun.technion.ac.il:~/hmuc/test  
ssh ofers@tamnun.technion.ac.il "cd hmuc;qsub -v bench=%1,arg=%2 hmuc.sh"
end: set /p tmp=Please wait:
