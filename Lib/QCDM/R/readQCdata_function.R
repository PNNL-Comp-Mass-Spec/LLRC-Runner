#' This function reads in the QC / Ratings Dataset
#'
#' @param cotDataName is the full filename of the curated dataset (csv)
#' @param outDataName is the full filename of the output Rdata object to
#'   contain the resulting datasets
#' @return An rdata object with the data objects needed for analyses
#'
#' @author Brett Amidan
#'
#' @examples
#' readQCdata(cotDataName="D:/Data/Data.csv",outDataName="D:/Data/Data.Rdata")

#' @export
readQCdata <- function(cotDataName,outDataName) {

  ###############  Read in the complete data
  cotdata <- read.table(cotDataName,sep=",",header=TRUE,row.names=NULL)
  rownames(cotdata) <- paste("pt",1:nrow(cotdata),sep="")

  P_2Anorm <- cotdata[,"P_2A"] / cotdata[,"DS_2B"]

  ## make binomial response
  BinomResp <- rep(0,nrow(cotdata))
  ind <- cotdata[,"Curated_Quality"]!="poor"
  BinomResp[ind] <- 1

  cotdata <- data.frame(cotdata,P_2Anorm,BinomResp)

  ## add other covariate data
  Acq_Length2 <- round(cotdata[,"Acq_Length"],-1)

  cotdata <- data.frame(cotdata,Acq_Length2)

  ## ID Quameter and SMAQC metrics
  ind <- is.element(substring(colnames(cotdata),1,3),c("XIC","RT_")) |
    is.element(substring(colnames(cotdata),1,5),c("MS1_T","MS1_C","MS1_F",
    "MS1_D","MS2_C","MS2_F","MS2_D","MS2_P"))
  q.metrics <- colnames(cotdata)[ind]

  ind <- is.element(substring(colnames(cotdata),1,2),c("C_","DS","IS","P_")) |
    is.element(substring(colnames(cotdata),1,5),c("MS1_1","MS1_2","MS1_3",
    "MS1_5","MS2_1","MS2_2","MS2_4"))
  smaq.metrics <- colnames(cotdata)[ind]

  ###################################
  #############  Output the Data
  save(list=c("cotdata","q.metrics","smaq.metrics"),file=outDataName)

  invisible()
} # ends function

