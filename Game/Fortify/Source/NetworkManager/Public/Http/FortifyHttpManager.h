// Copyright 2024, Jonathan Ogle-Barrington

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Http/FortifyHttpRequest.h"
#include "HttpBlueprintTypes.h"
#include "FortifyHttpManager.generated.h"

UCLASS(Abstract, Blueprintable)
class AFortifyHttpManager : public AActor
{
	GENERATED_BODY()
	
public:
	AFortifyHttpManager();

protected:
	virtual void BeginPlay() override;

public:	
	virtual void Tick(float DeltaTime) override;
	
	UFUNCTION(BlueprintCallable, Category = "HTTP Request", meta = (AutoCreateRefTerm = "BodyJson"))
	void CreateJsonRequest(const FJsonObjectWrapper& BodyJson, EHttpVerbs Verb, const FString& Path, const FOnHttpRequestResponse& OnResponse);

	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category = "HTTP Request")
	TSubclassOf<UFortifyHttpRequest> RequestClass;

	UFUNCTION(BlueprintImplementableEvent, Category = "HTTP Request")
	void TreatRequest(const FJsonObjectWrapper& BodyJson, EHttpVerbs Verb, const FString& Path, UFortifyHttpRequest* CreatedRequest);	

};
