#' This function performs Logistic Regression
#'
#' This function performs an individual LLRC and is called by CVlogisticReg2
#'
#' This function requires glmnet package
#'
#' @param data is the matrix of data to use
#' @param respName is the Y variable to be regressed on
#' @param cutoff is the desired LASSO parameter (lambda)
#' @return A list with coefficients and formula
#'
#' @author Brett Amidan

#' @export
logisticReg <- function(data,respName, cutoff=.01) {
  ## data contains all the potential predictors and the response
  ## respName is the colname of the response variable
  ## cutoff is used in the glmnet to cut off for coefficients

  indy <- colnames(data) != respName
  temp <- as.matrix(data[,indy])
  tempy <- data[,respName]

  #### perform GLM with Lasso to determine best coefficients
  gnet.out <- glmnet::glmnet(x=temp,y=tempy,family="binomial")
  # extract coefficients at a single value of lambda
  coef.vec <- as.numeric(coef(gnet.out,s=cutoff))
  # drop intercept
  coef.vec <- coef.vec[2:length(coef.vec)]
  indy <- coef.vec > 0 | coef.vec < 0
  keep.names <- colnames(temp)[indy]

  ### Put this into a logistic regression
  lr.formula <- as.formula(paste(respName, " ~ ",paste(keep.names,
    collapse=" + "),sep=""))
  q.log1 <- glm(lr.formula,data=data,family=binomial,maxit=50)
  q.pred1 <- predict(q.log1,data)
  predictions <- round(exp(q.pred1)/(exp(q.pred1)+1),2)
  coefficients <- q.log1$coef

  list(coef=coefficients,pred=predictions,formula=lr.formula)

} # ends function
