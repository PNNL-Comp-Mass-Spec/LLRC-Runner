#' CV Logistic Regression
#'
#' This function performs the cross validation (80/20) LLRC
#'
#' @param the.data is the matrix of data to use
#' @param respName is the Y variable to be regressed on
#' @param cutoff is the desired LASSO parameter (lambda)
#' @return A list with coefficients, predictions, actuals
#'
#' @author Brett Amidan

#' @export
CVlogisticReg2 <- function(the.data,respName, cutoff=.01) {

  ## data contains all the potential predictors and the response
  ## respName is the colname of the response variable
  ## cutoff is used in the glmnet to cut off for coefficients
  ## data divided into 5 parts 80/20 cross validation
  
  ## remove columns of data with too many NAs
  ind <- colSums(!is.na(the.data))/nrow(the.data) > .60
  the.data <- the.data[,ind]
  ## remove any rows that are NAs
  ind2 <- !is.na(rowSums(the.data))
  the.data <- the.data[ind2,]
  
  ## get random data orders
  samp.order <- sample(1:nrow(the.data),size=nrow(the.data))
  cutval <- nrow(the.data)/5
  groups <- vector(length=5,mode="list")
  groups[[1]] <- sort(samp.order[1:round(cutval,0)])
  groups[[2]] <- sort(samp.order[(round(cutval,0)+1):round(cutval*2,0)])
  groups[[3]] <- sort(samp.order[(round(cutval*2,0)+1):round(cutval*3,0)])
  groups[[4]] <- sort(samp.order[(round(cutval*3,0)+1):round(cutval*4,0)])
  groups[[5]] <- sort(samp.order[(round(cutval*4,0)+1):nrow(the.data)])

  ## Do the Lasso Calculations
  lr.out <- logisticReg(data=the.data,respName="BinomResp",cutoff=cutoff)

  ## setup output
  pred.vec <- rep(NA,nrow(the.data))
  names(pred.vec) <- rownames(the.data)
  for (i in 1:5) {
    test.rows <- groups[[i]]
    train.rows <- sort(c(1:nrow(the.data))[-groups[[i]]])
    ## get the data
    train.data <- the.data[train.rows,]
    test.data <- the.data[test.rows,]
    ## use Lasso results to make the model
    log.out <- glm(lr.out$formula,data=train.data,family="binomial",maxit=50)
    ## predict for your test.data
    pred1 <- predict(log.out,test.data)
    ## if too big, you can't take exp
    ind <- pred1 > 100
    pred1[ind] <- 100
    predictions <- round(exp(pred1)/(exp(pred1)+1),2)
    ## store predictions
    pred.vec[test.rows] <- predictions
  }

  ## output
  list(coef=lr.out$coef,pred=pred.vec,actual=the.data[,"BinomResp"])
  
} # ends function
