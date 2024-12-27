// Copyright 2024, Jonathan Ogle-Barrington

#include "Http/FortifyHttpManager.h"
#include "Http/FortifyHttpRequest.h" 

AFortifyHttpManager::AFortifyHttpManager()
{
	PrimaryActorTick.bCanEverTick = true;
}

void AFortifyHttpManager::BeginPlay()
{
	Super::BeginPlay();
}

void AFortifyHttpManager::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);
}

void AFortifyHttpManager::CreateJsonRequest(const FJsonObjectWrapper& BodyJson, EHttpVerbs Verb, const FString& Path, const FOnHttpRequestResponse& OnResponse)
{
	if (RequestClass == nullptr) 
	{
		UE_LOG(LogTemp, Warning, TEXT("Log: RequestClass default value isn't set in BP_HttpManager blueprint. Set RequestClass to BP_HttpRequest"));
		return;
	}
	UFortifyHttpRequest* RequestObject = NewObject<UFortifyHttpRequest>(this, RequestClass);
	RequestObject->OnResponseDelegate = OnResponse;
	TreatRequest(BodyJson, Verb, Path, RequestObject);
}