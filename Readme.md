
# LLRC Runner

This program uses LLRC to compute QCDM values using QC metric values from Quameter and SMAQC (Software Metrics for Analysis of Quality Control)

# Requirements

You must install the LLRC library into R prior to running LLRC. Steps:

1) Install R R-4.x (for example, R-4.1.1)
* Use defaults (core, 32-bit, and 64-bit)

2) Install the `glmnet` package
* Start R
  * Either use the GUI
  * Or start via the command line:
    * `C:\Program Files\R\R-4.1.1\bin\x64\R.exe`

* Install the package
  * `> install.packages(c("glmnet"), repos='https://cran.revolutionanalytics.com/')`

* Exit R
  * `> quit()`

3) Install `QCDM_2020.05.04.tar.gz` into R
* Option 1: use the command prompt
  * `"C:\Program Files\R\R-4.1.1\bin\R.exe" CMD INSTALL QCDM_2020.05.04.tar.gz`
  * If you see a permissions error, update the permissions on C:\Program Files\R\R-4.1.1\library as discussed above

* Option 2: use the GUI
  * Start the 64-bit R GUI
  * Choose Packages, then "Install packages from local zip file"
  * Choose file QCDM_2020.05.04.tar.gz
  * If prompted "Would you like to use a personal library instead?", choose "No"
     * Next, update "C:\Program Files\R\R-4.1.1\library" to grant "Write" access (you only need "Write", not "Modify")
     * Now try again with menu item "Install packages from local zip file"


## Console Switches

Syntax:

```
LLRCRunner.exe
   DatasetIDList [/W:WorkingDirectory] 
   [/DB] [/Skip]
```

DatasetIDList can be a single DatasetID, a list of DatasetIDs separated by commas, a range of DatasetIDs separated with a dash, or a timespan in hours. Examples:

| Command                               | Description                                                  |
|---------------------------------------|--------------------------------------------------------------|
| `LLRCRunner.exe 325145`               | Process a single dataset                                     |
| `LLRCRunner.exe 325145,325146,325150` | Process 3 datasets                                           |
| `LLRCRunner.exe 325145-325150`        | Process a range of 6 datasets                                |
| `LLRCRunner.exe 24h`                  | Process all "new" datasets added to DMS in the last 24 hours |

"New" datasets are those with null QCDM values in the `T_Dataset_QC` table

Use `/W` to specify the working directory path
* The default is the directory with the .exe
* The working directory must have files `Models_paper.Rdata` and `allData_v4.Rdata`

Use `/DB` to post the LLRC results to the database
* Calls stored procedure `StoreQCDMResults`

Use `/Skip` to skip datasets that already have a QCDM value defined

## Contacts

Written by  Joshua Davis and Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov\
Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics

## License

The LLRC Runner is licensed under the 2-Clause BSD License; 
you may not use this program except in compliance with the License.  You may obtain 
a copy of the License at https://opensource.org/licenses/BSD-2-Clause

Copyright 2023 Battelle Memorial Institute
