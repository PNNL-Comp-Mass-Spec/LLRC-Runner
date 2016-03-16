require(QCDM)
outDataName <- "C:/DMS_Programs/LLRCRunner/allData_v4.Rdata"
outputFolder <- "C:/DMS_Programs/LLRCRunner/"
ncdataFilename <- "C:/DMS_Programs/LLRCRunner/data.csv"
noncuratedPrediction(ncdataFilename=ncdataFilename, modelsFile=paste(outputFolder,"Models_paper.Rdata",sep=""), dataFilename=outDataName,outputFolder=outputFolder)