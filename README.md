This repository contains several libraries which can be used to control the Mono debugger.

* Mono.Debugger.Soft: The Mono Soft Debugger low level API
* Mono.Debugging: Pluggable debugger API abstraction. It provides a common API to be used as frontend to different debuggers.
* Mono.Debugging.Soft: Mono.Debugging backend for the Mono Soft Debugger.

Dependencies
============

The libraries in this repository have external dependencies, specifically:

* cecil (git://github.com/mono/cecil.git)
* nrefactory (git://github.com/icsharpcode/NRefactory.git)

Those libraries must be cloned side by side with debugger-libs.