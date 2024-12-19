#!/bin/bash
## Copyright Epic Games, Inc. All Rights Reserved.
##
## Unreal Engine 4 AutomationTool setup script
##
## This is a convenience file that calls through to the main batchfile in the 
## UE4/Engine/Build/BatchFiles directory.  
## It will not work correctly if you copy it to a different location and run it.


bash ./Engine/Build/BatchFiles/RunUBT.sh "$@"
