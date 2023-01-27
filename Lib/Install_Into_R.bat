@echo off
echo Start R, then install package glmnet

"C:\Program Files\R\R-4.1.1\bin\x64\R.exe"
echo > install.packages(c("glmnet"), repos='https://cran.revolutionanalytics.com/')
echo > quit()
pause

echo Install QCDM using the .tar.gz file
"C:\Program Files\R\R-4.1.1\bin\R.exe" CMD INSTALL QCDM_2020.05.04.tar.gz

pause
