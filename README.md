#Cinegy HTTP RTP Gateway - an AMWA Labs Project

This tool is all about moving broadcast-oriented streams over HTTP - and is being developed as part of the AMWA Labs project to determine next-generation workflows for the cloud-connected broadcaster. We're developing it in the open, and the main purpose of this tool is for experimentation, so don't expect stability or support!

##What can I do with it?

You can grab HTTP wrapped MPEG2 Transport Streams, and then have them carefully re-timed and buffered before being re-emitted back out as a multicast packet. If you have a server which performs the opposite (reads MPEG2 TS packets, and pushes them down an HTTP connection) then you can use the benefits of HTTP and HTTPS to securely and reliably transport a stream over the internet.

##Sounds too good to be true!

It is - HTTP currently won't work for every scenario - you need a reasonable connection and latency to your source. However, we think it will work in a great deal of situations just fine - and it's firewall-friendly and well understood. We aim to prove it works well, and should be considered as a sensible way to move low-latency real-time streams of audio, video and data around for broadcasters. Or it will be really unstable in many cases and people will hate us. But at least we'll have evidence about what works and what does not.

##Where do I find out more?

Head over to the [AMWA website](http://www.amwa.tv/) and look up the AMWA Labs projects and get involved!

##Getting the tool

Just to make your life easier, we auto-build this using AppVeyor and push to NuGet - here is how we are doing right now: 

[![Build status](https://ci.appveyor.com/api/projects/status/3v7errp523yun172?svg=true)](https://ci.appveyor.com/project/cinegy/httprtpgateway)

You can check out the latest compiled binary from the master or pre-master code here:

[AppVeyor HttpRtpGateway Project Builder](https://ci.appveyor.com/project/cinegy/httprtpgateway/build/artifacts)