#' This function performs prediction
#'
#' This function predicts the non-curated data
#'  only works for selected instrument
#'
#' @param ncdataFilename is full filename of the noncurated dataset
#' @param modelsFile is the full filename of the models file produced earlier
#' @param dataFilename is the full filename of the Rdata dataset
#' @param outputFolder is the path to store the output
#' @param instrument is the instrument to run the predictions for
#' @return A vector containing the predictions
#'
#' @author Brett Amidan
#'
#' @examples
#' noncuratedPrediction(ncdataFilename="D:/Data/ncData.csv",modelsFile="D:/Results/Models_paper.Rdata",dataFilename="D:/Data/Data.Rdata",outputFolder="D:/Results/",instrument="VOrbitrap")

#' @export
noncuratedPrediction <- function(ncdataFilename,modelsFile,dataFilename,
  outputFolder) {

  ##############  Read in data
  noncurated.data <- read.table(ncdataFilename,sep=",",header=TRUE,row.names=NULL)
  indy <- noncurated.data[,"DS_2B"] == 0 | is.na(noncurated.data[,"DS_2B"])
  noncurated.data[indy,"DS_2B"] <- .01
  P_2Anorm <- noncurated.data[,"P_2A"] / noncurated.data[,"DS_2B"]
  noncurated.data <- data.frame(noncurated.data,P_2Anorm)
  
  ## Load the models
  load(modelsFile)

  ## load other data objects
  load(dataFilename)

  ## get list of instruments
  instruments <- unique(as.character(noncurated.data[,"Instrument_Category"]))
  ind <- is.element(instruments,names(models))
  instruments <- instruments[ind]
  
  ## output
  output <- NULL
  predictions2 <- NULL
  
  for (instrument in instruments) {

    ### get only instrument data
    indy <- noncurated.data[,"Instrument_Category"]==instrument
    the.data <- noncurated.data[indy,]

    ## remove any rows for any of the metrics (columns) used with NAs
    if (instrument=="Exactive") {
      cnames <- names(models[[instrument]]$q$coef)
    } else {
      cnames <- names(models[[instrument]]$full$coef)
    }
    cnames <- cnames[2:length(cnames)]
    
    indy.keep <- rowSums(is.na(the.data[,cnames]))==0
    the.data <- the.data[indy.keep,]
    
    ## predict
    if (instrument=="Exactive") {
      pred1 <- predict(models[[instrument]]$q,the.data)
    } else {
      pred1 <- predict(models[[instrument]]$full,the.data)
    }
    ## if too big, you can't take exp
    ind <- pred1 > 100
    pred1[ind] <- 100
    predictions <- round(exp(pred1)/(exp(pred1)+1),2)

    ## Add predictions to the.data and write out
    LLRC.Prediction <- predictions
    temp <- data.frame(the.data,LLRC.Prediction)
    output <- rbind(output,temp)
    predictions2 <- c(predictions2,predictions)
  } # ends instrument loop

  write.csv(output,file=paste(outputFolder,"TestingDataset.csv",sep=""),
    row.names=FALSE)

  output
}
