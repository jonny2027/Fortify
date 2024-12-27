// Copyright 2024, Jonathan Ogle-Barrington

#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "HttpHeader.h"
#include "JsonObjectWrapper.h"
#include "FortifyHttpRequest.generated.h"

DECLARE_DYNAMIC_DELEGATE_TwoParams(FOnHttpRequestResponse, bool, Success, const FString&, Body);

UCLASS(Blueprintable, BlueprintType)
class UFortifyHttpRequest : public UObject
{
	GENERATED_BODY()

public:

	UPROPERTY()
	FOnHttpRequestResponse OnResponseDelegate;
	
	UFUNCTION(BlueprintCallable, Category = "HTTP Request")
	void CallOnResponseDelegate(bool Success, const FString& Body);
};
