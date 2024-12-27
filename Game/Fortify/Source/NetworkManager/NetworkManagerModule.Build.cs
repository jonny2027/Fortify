// Copyright 2024, Jonathan Ogle-Barrington

using UnrealBuildTool; 

public class NetworkManagerModule : ModuleRules
{ 
    public NetworkManagerModule(ReadOnlyTargetRules Target) : base(Target)
    {
		PrivateDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "WebSockets", "HttpBlueprint", "OnlineSubsystem", "CoreOnline", "JsonUtilities", "OnlineSubsystemUtils", "OnlineSubsystemSteam" });
    } 
}