#' This is the Training function
#'
#' This function uses LASSO and Logistic regression and cross validation
#'  to determine the optimal lambda (lasso parameter) and
#'  tau (cutoff for kappa cost function)
#'
#' @param dataFilename is full filename of the Rdata object containing the data
#' @param outputFolder is the path to store the output
#' @param optKappa is the kappa to run the analyses on (loss function value)
#' @return A list containing the models used, the results (sensitivity etc),
#'   univariate.results (VOrbitrap vs single variables), optimal values of
#'   lambda, tau, and kappa for each equipment
#'
#' @author Brett Amidan
#'
#' @examples
#' trainingLLRC(dataFilename="D:/Data/Data.Rdata",outputFolder="D:/Results/",optKappa=5)

## This function requires glmnet library and local QCDM library

#' @export
trainingLLRC <- function(dataFilename,outputFolder,optKappa=5) {

  ## Get Data
  load(dataFilename)

  the.data <- cotdata
  ## flip the response so that poor = 1, good/ok = 0
  the.data[,"BinomResp"] <- abs(the.data[,"BinomResp"]-1)

  ## lambda and kappa
  lambda.vec <- seq(.01,.15,by=.02)
  kappa.vec <- c(1,3,5,10,15)

  equip.vec <- sort(unique(as.character(the.data[,"Instrument_Category"])))

  ## output
  output <- vector(mode="list",length=length(equip.vec))
  names(output) <- equip.vec

  ### Loop thru each equipment
  for (equip in equip.vec) {
    print(equip)
    ## select all data from equipment
    ind <- the.data[,"Instrument_Category"]==equip
    data2 <- the.data[ind,]
    data2 <- data2[,c(q.metrics,smaq.metrics,"BinomResp")]

    ## calculate proportions of 0 and 1 from truth
    prop1 <- sum(data2[,"BinomResp"])/nrow(data2)
    prop0 <- 1 - prop1

    ## create temp output
    temp.out <- matrix(NA,nrow=0,ncol=4)
    colnames(temp.out)<- c("lambda","kappa","tau","lossValue")

    ###  Loop thru each lambda
    for (lambda in lambda.vec) {
      ### Do the cross validation logistic regression using Lasso
      set.seed(1)
      lr.out <- CVlogisticReg2(the.data=data2,respName="BinomResp",cutoff=lambda)

      ## Calculate results using cost function
      for (kappa in kappa.vec) {
        ### Find the optimal cut given kappa
        scores <- seq(.01,.99,by=.01)
        lossVals <- NULL
        ## loop thru each tau
        for (i in scores) {
          ## count # predicting poor, but actually good
          ind1 <- lr.out$pred >= i & lr.out$actual == 0
          ## count # predicting good, but actually poor * kappa
          ind2 <- lr.out$pred < i & lr.out$actual == 1
          ## score using the loss function
          lossVals <- c(lossVals,(sum(ind1)+sum(ind2)*kappa)/
            length(lr.out$pred))
        }
        ### store the results
        temp1 <- cbind(rep(lambda,length(scores)),rep(kappa,length(scores)),
          scores,lossVals)
        temp.out <- rbind(temp.out,temp1)
      } # ends kappa loop
    } # ends lambda loop
    output[[equip]] <- temp.out
  } # ends equip loop


  ### print out the results for each equipment
  for (equip in equip.vec) {
    write.csv(output[[equip]],paste(outputFolder,"CostFunctionResults_",equip,
      ".csv",sep=""),row.names=FALSE)
  } # ends equip loop

  ### Loop by Kappa

  for (kappa in kappa.vec) {
  pdf(file=paste(outputFolder,"Loss Function Plots (kappa=",
    kappa,").pdf",sep=""),width=7,height=7)
  for (equip in equip.vec) {
    tempdata <- output[[equip]]
    ind <- tempdata[,"kappa"]==kappa
    plot(0,0,type="n",xlim=c(0,1),ylim=c(0,max(tempdata[ind,"lossValue"])),
      xlab="Tau (Threshold)",ylab="Loss Function Value",main=equip)

    ## loop thru each lambda
    count <- 0
    for (lambda in lambda.vec) {
      count <- count + 1
      indy <- tempdata[,"lambda"]==lambda & tempdata[,"kappa"]==kappa
      lines(tempdata[indy,"tau"],tempdata[indy,"lossValue"],col=count)
      indy2 <- tempdata[indy,"lossValue"] == min(tempdata[indy,"lossValue"])
      x.val <- tempdata[indy,"tau"][indy2]
      y.val <- tempdata[indy,"lossValue"][indy2]
      ## if tied, pick point closest to 0.5
      if (length(x.val)>1) {
        tt <- abs(x.val-.5)
        indy3 <- tt == min(tt)
        x.val <- x.val[indy3][1]
        y.val <- y.val[indy3][1]
      }
      points(x.val,y.val,col=count,pch=16)
    }# ends lambda loop
    ## legend
    legend("topleft",legend = lambda.vec,cex=.7,title="Lambda",col=1:8,pch=16,
      bg="white")
  } # ends equip loop
  dev.off()
  }

  ### Using lambda >= 0.05 and kappa = optKappa find optimal
  optimal <- vector(mode="list",length=length(equip.vec))
  names(optimal) <- equip.vec
  for (equip in equip.vec) {
    tempdata <- output[[equip]]
    indy <- tempdata[,"lambda"]>=0.05 & tempdata[,"kappa"]==optKappa
    tempdata <- tempdata[indy,]
    indy2 <- tempdata[,"lossValue"]== min(tempdata[,"lossValue"])
    if (sum(indy2)>1) {
      tt <- tempdata[indy2,]
      tt2 <- abs(tt[,"tau"]-.5)
      indy3 <- tt2 == min(tt2)
      optimal[[equip]] <- tt[indy3,]
    } else {
      optimal[[equip]] <- tempdata[indy2,]
    }
    if (is.null(dim(optimal[[equip]]))) {
      optimal[[equip]] <- matrix(optimal[[equip]],nrow=1)
      colnames(optimal[[equip]]) <- c("lambda","kappa","tau","lossValue")
    }
  } # ends equip loop

########################################################################
#####  Calculate Sensitivity and Specificity for each using optimal

  ## optimals
  opt.lambda <- c(min(optimal$Exactive[,"lambda"]),min(optimal$LTQ[,"lambda"]),
    min(optimal$Orb[,"lambda"]),min(optimal$VOrb[,"lambda"]))
  opt.tau <- c(min(optimal$Exactive[,"tau"]),min(optimal$LTQ[,"tau"]),
    min(optimal$Orb[,"tau"]),min(optimal$VOrb[,"tau"]))
  names(opt.lambda) <- names(opt.tau) <- sort(equip.vec)

  ## create the models
  models <- createModels(opt.lambda=opt.lambda,cotdata=cotdata,
    outputFolder=outputFolder,smaq.metrics=smaq.metrics,
    q.metrics=q.metrics)

  model.list <- list(models$Exactive$q,models$LTQ_IonTrap$full,
    models$Orbitrap$full,models$VOrbitrap$full)
  names(model.list) <- equip.vec

  ## output
  log.pred <- vector(mode="list",length=length(equip.vec))
  names(log.pred) <- equip.vec

  ### Loop thru each equipment
  for (equip in equip.vec) {
    print(equip)
    coef.names <- names(model.list[[equip]]$coef)
    coef.names <- coef.names[2:length(coef.names)]

    ## select all data from equipment
    ind <- the.data[,"Instrument_Category"]==equip
    data2 <- the.data[ind,]
    data2 <- data2[,c(coef.names,"BinomResp")]
    ## remove any with NA's
    ind.na <- rowSums(is.na(data2)) > 0
    data2 <- data2[!ind.na,]

    ## get random data orders
    set.seed(1)
    samp.order <- sample(1:nrow(data2),size=nrow(data2))
    cutval <- nrow(data2)/5
    groups <- vector(length=5,mode="list")
    groups[[1]] <- sort(samp.order[1:round(cutval,0)])
    groups[[2]] <- sort(samp.order[(round(cutval,0)+1):round(cutval*2,0)])
    groups[[3]] <- sort(samp.order[(round(cutval*2,0)+1):round(cutval*3,0)])
    groups[[4]] <- sort(samp.order[(round(cutval*3,0)+1):round(cutval*4,0)])
    groups[[5]] <- sort(samp.order[(round(cutval*4,0)+1):nrow(data2)])

    ## setup output
    pred.vec <- rep(NA,nrow(data2))
    names(pred.vec) <- rownames(data2)
    for (i in 1:5) {
      test.rows <- groups[[i]]
      train.rows <- sort(c(1:nrow(data2))[-groups[[i]]])
      ## get the data
      train.data <- data2[train.rows,]
      test.data <- data2[test.rows,]
      ## use Lasso results to make the model
      log.out <- glm(model.list[[equip]]$formula,data=train.data,
        family="binomial",maxit=50)
      ## predict for your test.data
      pred1 <- predict(log.out,test.data)
      ## if too big, you can't take exp
      ind <- pred1 > 100
      pred1[ind] <- 100
      predictions <- round(exp(pred1)/(exp(pred1)+1),2)
      ## store predictions
      pred.vec[test.rows] <- predictions
    }
    outmat <- cbind(pred.vec,data2[,"BinomResp"])
    colnames(outmat) <- c("Pred","Actual")
    log.pred[[equip]] <- outmat
  } # ends equip loop

  ### put together the data used in training
  all.pred <- NULL
  for (i in 1:length(log.pred)) {
    all.pred <- rbind(all.pred,log.pred[[i]])
  }
  colnames(all.pred) <- c("LLRC.Prediction","Actual")
  temp <- data.frame(the.data[rownames(all.pred),],all.pred)
  indy <- is.element(colnames(temp),c("Acq_Length2","Actual"))
  temp <- temp[,!indy]

  write.csv(temp,file=paste(outputFolder,"TrainingDataset.csv",sep=""),
    row.names=FALSE)
  training <- temp

  ################  Create the plot
  #jpeg(file="//pnl/projects/SQM/Documents/Amidan/Results_Paper/CV_Results.jpg",
  #  res=300,units="in",width=6,height=6)
  tiff(file=paste(outputFolder,"CV_Results.tif",sep=""),compression="none")

  par(mfrow=c(2,2))
  par(las=1)
  plot.names <- c("Exactive","LTQ-Ion Trap","LTQ-Orbitrap","Velos-Orbitrap")
  count <- 0
  for (equip in equip.vec) {
    count <- count + 1
    results <- log.pred[[equip]]
    cutoffs <- seq(.01,.99,by=.01)
    res <- matrix(NA,nrow=length(cutoffs),ncol=3)
    colnames(res) <- c("Cutoff","Sensitivity","Specificity")
    res[,1] <- cutoffs
    for (i in 1:length(cutoffs)) {
      ## positive = Poor
      pred.pos <- results[,"Pred"] > cutoffs[i]
      truth.pos <- results[,"Actual"] == 1
      ### Calculate Sensitivity (# of true positives (poors)/ (# true positives+
      ###   number of false negatives
      numerator <- sum(pred.pos & truth.pos)
      denom <- numerator + sum(!pred.pos & truth.pos)
      res[i,"Sensitivity"] <- numerator / denom
      ### Calculate Specificity (# of true negatives(good/ok)/ (# true negatives +
      ###   number of false positives
      numerator <- sum(!pred.pos & !truth.pos)
      denom <- numerator + sum(pred.pos & !truth.pos)
      res[i,"Specificity"] <- numerator / denom
    }
    ## Do the plot
    plot(0,0,type="n",xlim=c(0,1), ylim=c(0,1),main=plot.names[count],
      xlab="Score (Threshold Decision)",ylab="Probability Correctly Identified")
    lines(res[,1],res[,"Sensitivity"],col=2,lwd=3,lty=2)
    lines(res[,1],res[,"Specificity"],col=3,lwd=3)
    abline(v=opt.tau[equip],col="gray",lty=3,lwd=3)
    legend(x="bottomleft",legend=c("Sensitivity","Specificity"),col=c(2:3),
      lwd=3,lty=c(3,1),bg="white")
  }
  dev.off()

  ### sensitivity & specificity calculations
  results <- matrix(NA,nrow=5,ncol=length(equip.vec))
  rownames(results) <- c("sensitivity","specificity","n.total",
    "n.good","n.bad")
  colnames(results) <- equip.vec
  ## Exactive
  # sensitivity
  indy <- log.pred$Ex[,"Pred"] > opt.tau["Exactive"]
  indy1 <- log.pred$Ex[,"Actual"]==1
  results["sensitivity","Exactive"] <- sum(indy & indy1)/(sum(indy & indy1)+
    sum(!indy & indy1))
  # specificity
  results["specificity","Exactive"] <- sum(!indy & !indy1)/(sum(!indy & !indy1)+
    sum(indy & !indy1))
  results["n.total","Exactive"] <- nrow(log.pred$Ex)
  results["n.bad","Exactive"] <- sum(indy1)
  results["n.good","Exactive"] <- sum(!indy1)

  ## LTQ
  # sensitivity
  indy <- log.pred$LTQ[,"Pred"] > opt.tau["LTQ_IonTrap"]
  indy1 <- log.pred$LTQ[,"Actual"]==1
  results["sensitivity","LTQ_IonTrap"] <- sum(indy & indy1)/(sum(indy & indy1)+
    sum(!indy & indy1))
  # specificity
  results["specificity","LTQ_IonTrap"] <- sum(!indy & !indy1)/(sum(!indy & !indy1)+
    sum(indy & !indy1))
  results["n.total","LTQ_IonTrap"] <- nrow(log.pred$LTQ)
  results["n.bad","LTQ_IonTrap"] <- sum(indy1)
  results["n.good","LTQ_IonTrap"] <- sum(!indy1)

  ## Orbitrap
  # sensitivity
  indy <- log.pred$Orb[,"Pred"] > opt.tau["Orbitrap"]
  indy1 <- log.pred$Orb[,"Actual"]==1
  results["sensitivity","Orbitrap"] <- sum(indy & indy1)/(sum(indy & indy1)+
    sum(!indy & indy1))
  # specificity
  results["specificity","Orbitrap"] <- sum(!indy & !indy1)/(sum(!indy & !indy1)+
    sum(indy & !indy1))
  results["n.total","Orbitrap"] <- nrow(log.pred$Orb)
  results["n.bad","Orbitrap"] <- sum(indy1)
  results["n.good","Orbitrap"] <- sum(!indy1)

  ## VOrbi
  # sensitivity
  indy <- log.pred$VOrb[,"Pred"] > opt.tau["VOrbitrap"]
  indy1 <- log.pred$VOrb[,"Actual"]==1
  results["sensitivity","VOrbitrap"] <- sum(indy & indy1)/(sum(indy & indy1)+
    sum(!indy & indy1))
  # specificity
  results["specificity","VOrbitrap"] <- sum(!indy & !indy1)/(sum(!indy & !indy1)+
    sum(indy & !indy1))
  results["n.total","VOrbitrap"] <- nrow(log.pred$VOrb)
  results["n.bad","VOrbitrap"] <- sum(indy1)
  results["n.good","VOrbitrap"] <- sum(!indy1)

  #####################################
  ###  Look at VOrbitrap results for logistic and univariate

  indy <- the.data[,"Instrument_Category"]=="VOrbitrap"
  vorb.data <- the.data[indy,]

  vars <- c("P_2C","P_2A","RT_TIC_Q2","MS1_2B")
  output5 <- matrix(NA,nrow=length(vars),ncol=2)
  rownames(output5) <- vars
  colnames(output5) <- c("FalsePositiveRate","FalseNegativeRate")

  for (i in 1:length(vars)) {
    values <- seq(min(vorb.data[,vars[i]],na.rm=TRUE),max(vorb.data[,vars[i]],
      na.rm=TRUE),length=100)
    temp <- matrix(NA,nrow=length(values),ncol=2)
    for (j in 1:length(values)) {
      ind.na <- is.na(vorb.data[,vars[i]])
      indy <- vorb.data[!ind.na,vars[i]] <= values[j]
      fp <- sum(indy & vorb.data[!ind.na,"BinomResp"]==0) /
        sum(vorb.data[!ind.na,"BinomResp"]==0)
      fn <- sum(!indy & vorb.data[!ind.na,"BinomResp"]==1) /
        sum(vorb.data[!ind.na,"BinomResp"]==1)
      temp[j,] <- c(fp,fn)
    }
    val <- 1-results["sensitivity","VOrbitrap"]
    output5[i,2] <- val
    output5[i,1] <- approx(x=temp[,2],y=temp[,1],val)$y
  }

  ## output
  list(models=models,univariate.results=output5,results=results,
    optimal.lambda=opt.lambda,optimal.tau=opt.tau,kappa=optKappa,
    training=training)

} # ends function
