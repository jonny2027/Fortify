// Copyright Epic Games, Inc. All Rights Reserved.

#include "Blueprint/TG_AsyncExportTask.h"
#include "Misc/Paths.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "TG_Graph.h"
#include "UObject/Package.h"
#include "Model/Mix/MixSettings.h"

UTG_AsyncExportTask::UTG_AsyncExportTask(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
{

}

UTG_AsyncExportTask* UTG_AsyncExportTask::TG_AsyncExportTask( UTextureGraph* InTextureGraph, const bool OverwriteTextures, const bool bSave)
{
	UTG_AsyncExportTask* Task = NewObject<UTG_AsyncExportTask>();
	Task->SetFlags(RF_Standalone);
	Task->OverwriteTextures = OverwriteTextures;
	Task->bSave = bSave;

	if (InTextureGraph != nullptr)
	{
		Task->OrignalTextureGraphPtr = InTextureGraph;
		Task->TextureGraphPtr = (UTextureGraph*)StaticDuplicateObject(Task->OrignalTextureGraphPtr, GetTransientPackage(), NAME_None, RF_Standalone, UTextureGraph::StaticClass());
		FTG_HelperFunctions::InitTargets(Task->TextureGraphPtr);
		Task->RegisterWithTGAsyncTaskManger();
	}
	
	return Task;
}

void UTG_AsyncExportTask::Activate()
{
	// Start the async task on a new thread
	Super::Activate();
	UE_LOG(LogTextureGraph, Log, TEXT("TG_AsyncExportTask:: Activate"));

	if (!IsValid(TextureGraphPtr))
	{
		UE_LOG(LogTextureGraph, Warning, TEXT("TG_AsyncExportTask::Cannot export Texture Graph not selected"));
		return;
	}

	TargetExportSettings = FExportSettings();
	TargetExportSettings.OnDone.BindUFunction(this, "OnExportDone");

	FTG_HelperFunctions::ExportAsync(TextureGraphPtr, "", "", TargetExportSettings, false, OverwriteTextures, true, bSave);
}

void UTG_AsyncExportTask::OnExportDone()
{
	TargetExportSettings.ExportPreset.clear();
	TargetExportSettings.OnDone.Unbind();
	OnDone.Broadcast();
	TextureGraphPtr->FlushInvalidations();
	ClearFlags(RF_Standalone);
	SetReadyToDestroy();
}

void UTG_AsyncExportTask::FinishDestroy()
{
	if (TextureGraphPtr != nullptr)
	{
		TextureGraphPtr->GetSettings()->FreeTargets();
		TextureGraphPtr->ClearFlags(RF_Standalone);
		TextureGraphPtr = nullptr;
		OrignalTextureGraphPtr = nullptr;
	}
	Super::FinishDestroy();
}