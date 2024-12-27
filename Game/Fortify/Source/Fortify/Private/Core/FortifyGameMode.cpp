// Copyright Epic Games, Inc. All Rights Reserved.

#include "Core/FortifyGameMode.h"
#include "Player/FortifyCharacter.h"
#include "UObject/ConstructorHelpers.h"

AFortifyGameMode::AFortifyGameMode()
{
	// set default pawn class to our Blueprinted character
	static ConstructorHelpers::FClassFinder<APawn> PlayerPawnBPClass(TEXT("/Game/ThirdPerson/Blueprints/BP_ThirdPersonCharacter"));
	if (PlayerPawnBPClass.Class != NULL)
	{
		DefaultPawnClass = PlayerPawnBPClass.Class;
	}
}
