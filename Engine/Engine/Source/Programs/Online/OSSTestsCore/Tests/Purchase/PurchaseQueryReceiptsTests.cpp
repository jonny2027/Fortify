// Copyright Epic Games, Inc. All Rights Reserved.

#include "TestDriver.h"
#include "TestUtilities.h"

#include "OnlineSubsystemCatchHelper.h"

#include "Helpers/Purchase/PurchaseQueryReceiptsHelper.h"
#include "Helpers/Purchase/PurchaseGetReceiptsHelper.h"
#include "Helpers/Purchase/PurchaseCheckoutHelper.h"
#include "Helpers/Identity/IdentityGetUniquePlayerIdHelper.h"

#define PURCHASE_TAG "[suite_purchase]"
#define EG_PURCHASE_QUERYRECEIPTS_TAG PURCHASE_TAG "[queryreceipts]"

#define PURCHASE_TEST_CASE(x, ...) ONLINESUBSYSTEM_TEST_CASE(x, PURCHASE_TAG __VA_ARGS__)

PURCHASE_TEST_CASE("Verify calling QueryReceipts with valid inputs returns the expected result(Success Case)", EG_PURCHASE_QUERYRECEIPTS_TAG)
{
	int32 LocalUserNum = 0;
	FUniqueNetIdPtr LocalUserId = nullptr;
	bool LocalRestoreReceipts = true; //this param doesen't use in related step
	const FUniqueOfferId& LocalOfferId = TEXT("Item1_Id");
	const FString LocalItemName = TEXT("Cool Item1");
	int32 LocalQuantity = 1;
	bool bLocalIsConsumable = true;
	const FOfferNamespace LocalOfferNamespace = TEXT("");
	FPurchaseCheckoutRequest PurchCheckRequest = {};
	PurchCheckRequest.AddPurchaseOffer(LocalOfferNamespace, LocalOfferId, LocalQuantity, bLocalIsConsumable);
	int32 NumUsersToImplicitLogin = 1;

	GetLoginPipeline(NumUsersToImplicitLogin)
		.EmplaceStep<FIdentityGetUniquePlayerIdStep>(LocalUserNum, [&LocalUserId](FUniqueNetIdPtr InUserId) {LocalUserId = InUserId; })
		.EmplaceStep<FPurchaseCheckoutStep>(&LocalUserId, &PurchCheckRequest, LocalOfferId, LocalItemName)
		.EmplaceStep<FPurchaseQueryReceiptsStep>(&LocalUserId, LocalRestoreReceipts, LocalOfferId)
		.EmplaceStep<FPurchaseGetReceiptsStep>(&LocalUserId, LocalOfferId, LocalItemName);

	RunToCompletion();
}