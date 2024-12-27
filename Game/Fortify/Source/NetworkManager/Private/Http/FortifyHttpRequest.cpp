// Copyright 2024, Jonathan Ogle-Barrington

#include "Http/FortifyHttpRequest.h"
#include "HttpHeader.h"

void UFortifyHttpRequest::CallOnResponseDelegate(bool Success, const FString& Body)
{
	OnResponseDelegate.ExecuteIfBound(Success, Body);
}
