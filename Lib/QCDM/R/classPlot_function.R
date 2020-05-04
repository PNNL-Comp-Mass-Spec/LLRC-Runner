#' This does a scatterplot according to the classifications
#'
#' @param data is the matrix of data to use
#' @param Xvar is variable from data to be plotted on x axis
#' @param Yvar is variable from data to be plotted on y axis
#' @param classVar is variable to plot by
#' @param classes are the specific classes to plot (default to all)
#' @param legPos is place for legend (default "bottomright")
#' @param col.vec and pch.vec are for points on plot
#' @return A plot
#'
#' @author Brett Amidan

#' @export
classPlot <- function(data,Xvar,Yvar,classVar,classes=NULL,legPos="bottomright",
  leg.cex=.8,col.vec=c(2,3,4,6,1,7),pch.vec=c(1,2,3,4,6,9),...) {

  plot(data[,Xvar],data[,Yvar],type="n",xlab=Xvar,ylab=Yvar,...)

  ## get classes
  if (is.null(classes)) classes <- unique(as.character(data[,classVar]))
  
  for (i in 1:length(classes)) {
    indy <- data[,classVar] == classes[i]
    points(data[indy,Xvar],data[indy,Yvar],col=col.vec[i],pch=pch.vec[i])
  } # ends i loop

  ## legend
  legend(x=legPos,legend=classes,col=col.vec[1:length(classes)],
    pch=pch.vec[1:length(classes)],cex=leg.cex)

  invisible()
} # ends function
