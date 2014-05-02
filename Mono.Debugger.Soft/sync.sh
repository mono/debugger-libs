#!/bin/bash

if [ -d ../../../../../mono ]; then
    MONO_DIR=../../../../../mono
else
    MONO_DIR=../mono
fi

/bin/cp -f $MONO_DIR/mcs/class/Mono.Debugger.Soft/Mono.Debugger.Soft/*.cs Mono.Debugger.Soft/
cd $MONO_DIR
revision=`git log | head -1 | awk '{print $2}'`
cd - > /dev/null
echo $revision > mono-git-revision
