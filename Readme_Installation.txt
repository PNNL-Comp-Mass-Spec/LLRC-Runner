You must install the LLRC library into R prior to running LLRC.  Steps:

1) Install R R-2.15.3
	a. \\roadrunnerold\PAST\Software\R
	b. Use defaults (core, 32-bit, and 64-bit)

2) Install QCDM_2013.07.18 into R
	Option 1
		a. Start the 64-bit R Gui
		b. Choose Packages, then "Install packages from local zip file"
		c. Choose file QCDM_2013.07.18.zip
		d. If prompted "Would you like to use a personal library instead?", choose "No"
			i. Next, update "C:\Program Files\R\R-2.15.3\library" to grant "Write" access (you only need "Write", not "Modify")

	Option 2:
		a. Run this at the command prompt:
			"C:\Program Files\R\R-2.15.3\bin\R.exe" CMD INSTALL QCDM_2013.07.18.zip
		b. If you see a permissions error, update the permissions on C:\Program Files\R\R-2.15.3\library as discussed above

3) Run LLRCRunner.exe
	LLRCRunner.exe 311292
	 or
	LLRCRunner.exe 311104-311300
	 or
	LLRCRunner.exe 24h
