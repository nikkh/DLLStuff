# DLLStuff

This is the result of investigation that one of my colleagues asked me to look into.  

In essence can I create a utility that
* can accept an command message
* dynamically load a function from a DLL (both specified in the message)
* execute the function with parameters passed in the message.  
* can be containerised
* can be hosted on a number of container related platforms?

The answer to all the questions above is a resounding **YES**, but unfortunately the preferred container hosting platfrom is Azure Container Instances, and there is a little glitch there at the moment (that should be resolved very soon). 

This repo contains everything you need to get you own example up and running...

## Getting Started
