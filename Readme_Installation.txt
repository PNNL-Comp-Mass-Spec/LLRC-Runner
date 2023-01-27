You must install the LLRC library into R prior to running LLRC.  Steps:

1) Install R R-4.x (for example, R-4.1.1)
	a. \\roadrunnerold\PAST\Software\R
	b. Use defaults (core, 32-bit, and 64-bit)

2) Install the glmnet package
	a. Start R
		i. Either use the GUI
		ii. Or start via the command line:
		    "C:\Program Files\R\R-4.1.1\bin\x64\R.exe"

	b. Install the package
	   > install.packages(c("glmnet"), repos='https://cran.revolutionanalytics.com/')

	c. Exit R
       > quit()

3) Install QCDM_2020.05.04.tar.gz into R
	Option 1
		a. Start the 64-bit R GUI
		b. Choose Packages, then "Install packages from local zip file"
		c. Choose file QCDM_2020.05.04.tar.gz
		d. If prompted "Would you like to use a personal library instead?", choose "No"
			i. Next, update "C:\Program Files\R\R-4.1.1\library" to grant "Write" access (you only need "Write", not "Modify")
			ii. Now try again with menu item "Install packages from local zip file"

	Option 2:
		a. Run this at the command prompt:
            "C:\Program Files\R\R-4.1.1\bin\R.exe" CMD INSTALL QCDM_2020.05.04.tar.gz

		b. If you see a permissions error, update the permissions on C:\Program Files\R\R-4.1.1\library as discussed above

4) Run LLRCRunner.exe
	LLRCRunner.exe 311292
	 or
	LLRCRunner.exe 311104-311300
	 or
	LLRCRunner.exe 24h
