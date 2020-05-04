#' This function performs Logistic Regression on the Qc Data
#'
#' @param opt.lambda is the optimal or desired lambda for the LLRC
#' @param cotdata is the curated dataset
#' @param outputFolder is the path to store results
#' @param smaq.metrics are the smaq metrics
#' @param q.metrics are the q metrics
#' @return LLRC models for each equipment
#'
#' @author Brett Amidan

#' @export
createModels <- function(opt.lambda,cotdata,outputFolder,smaq.metrics,q.metrics) {

  ## flip the response so that poor = 1, good/ok = 0
  cotdata[,"BinomResp"] <- abs(cotdata[,"BinomResp"]-1)

  ## Get list of all possible equipment
  equip <- unique(as.character(cotdata[,"Instrument_Category"]))
  models <- vector(mode="list",length=(length(equip)+1))
  names(models) <- c(equip,"full")

  ## loop thru each equip and fit models
  for (i in equip) {
    indy <- as.character(cotdata[,"Instrument_Category"])==i
    edata <- cotdata[indy,]
    ## only fit with good/poor
    ind1 <- is.element(edata[,"Curated_Quality"],c("good","poor"))
    edata <- edata[ind1,]

    ####  Full model, full equip data
    ## id rows with NA's
    ind1b <- rowSums(!is.na(edata))/ncol(edata)>.95
    edata2 <- edata[ind1b,]

    tempout <- vector(mode="list",length=2)
    names(tempout) <- c("full","q")

    if (nrow(edata2)>30) {
      ## remove any columns with NAs
      ind1c <- colSums(is.na(edata2))>0
      edata2 <- edata2[,!ind1c]

      ####### Log Reg on full data
      indy <- is.element(colnames(edata2),c(smaq.metrics,q.metrics,"BinomResp"))
      temp.lr <- logisticReg(data=edata2[,indy],respName="BinomResp",
        cutoff=opt.lambda[i])
      tempout[[1]] <- glm(temp.lr$formula,data=edata2,family="binomial",maxit=50)
    }
    ####  Q metric model, full equip data
    edata2 <- edata[,c(q.metrics,"BinomResp")]
    ind1b <- rowSums(!is.na(edata2))/ncol(edata2)>.50
    edata2 <- edata2[ind1b,]
    ## remove any columns with NAs
    ind1c <- colSums(is.na(edata2))>0
    edata2 <- edata2[,!ind1c]
    if (nrow(edata2)>30) {
      temp.lr <- logisticReg(data=edata2,respName="BinomResp",
        cutoff=opt.lambda[i])
      tempout[[2]] <- glm(temp.lr$formula,data=edata2,family="binomial",maxit=50)
    }
    ## assign
    models[[i]] <- tempout
  }

  ###########  Add Full Model (no equip)
  ## only fit with good/poor
  ind1 <- is.element(cotdata[,"Curated_Quality"],c("good","poor"))
  data1 <- cotdata[ind1,c(q.metrics,smaq.metrics,"BinomResp")]
  ## id rows with NA's
  ind1b <- rowSums(!is.na(data1))/ncol(data1)>.80
  data2 <- data1[ind1b,]
  ## remove any columns with NAs
  ind1c <- colSums(is.na(data2))>0
  data2 <- data2[,!ind1c]
  tempout <- vector(mode="list",length=2)
  names(tempout) <- c("full","q")
  ## full model
  indy <- is.element(colnames(data2),c(smaq.metrics,q.metrics,"BinomResp"))
  temp.lr <- logisticReg(data=data2[,indy],respName="BinomResp",cutoff=.05)
  tempout[[1]] <- glm(temp.lr$formula,data=data2,family="binomial",maxit=50)

  ## q model
  data2 <- data1[,c(q.metrics,"BinomResp")]
  ## id rows with NA's
  ind1b <- rowSums(!is.na(data1))/ncol(data1)>.80
  data2 <- data1[ind1b,]
  ## remove any columns with NAs
  ind1c <- colSums(is.na(data2))>0
  data2 <- data2[,!ind1c]
  temp.lr <- logisticReg(data=data2,respName="BinomResp",cutoff=.05)
  tempout[[2]] <- glm(temp.lr$formula,data=data2,family="binomial",maxit=50)
  models[[length(models)]] <- tempout

  #### Save the models
  save(list=c("models"),
    file=paste(outputFolder,"Models_paper.Rdata",sep=""))

  models
} # ends function

