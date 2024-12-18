// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

// IWYU pragma: private, include "FortifyGameMode.h"
#include "UObject/ObjectMacros.h"
#include "UObject/ScriptMacros.h"

PRAGMA_DISABLE_DEPRECATION_WARNINGS
#ifdef FORTIFY_FortifyGameMode_generated_h
#error "FortifyGameMode.generated.h already included, missing '#pragma once' in FortifyGameMode.h"
#endif
#define FORTIFY_FortifyGameMode_generated_h

#define FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_12_INCLASS_NO_PURE_DECLS \
private: \
	static void StaticRegisterNativesAFortifyGameMode(); \
	friend struct Z_Construct_UClass_AFortifyGameMode_Statics; \
public: \
	DECLARE_CLASS(AFortifyGameMode, AGameModeBase, COMPILED_IN_FLAGS(0 | CLASS_Transient | CLASS_Config), CASTCLASS_None, TEXT("/Script/Fortify"), FORTIFY_API) \
	DECLARE_SERIALIZER(AFortifyGameMode)


#define FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_12_ENHANCED_CONSTRUCTORS \
private: \
	/** Private move- and copy-constructors, should never be used */ \
	AFortifyGameMode(AFortifyGameMode&&); \
	AFortifyGameMode(const AFortifyGameMode&); \
public: \
	DECLARE_VTABLE_PTR_HELPER_CTOR(FORTIFY_API, AFortifyGameMode); \
	DEFINE_VTABLE_PTR_HELPER_CTOR_CALLER(AFortifyGameMode); \
	DEFINE_DEFAULT_CONSTRUCTOR_CALL(AFortifyGameMode) \
	FORTIFY_API virtual ~AFortifyGameMode();


#define FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_9_PROLOG
#define FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_12_GENERATED_BODY \
PRAGMA_DISABLE_DEPRECATION_WARNINGS \
public: \
	FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_12_INCLASS_NO_PURE_DECLS \
	FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_12_ENHANCED_CONSTRUCTORS \
private: \
PRAGMA_ENABLE_DEPRECATION_WARNINGS


template<> FORTIFY_API UClass* StaticClass<class AFortifyGameMode>();

#undef CURRENT_FILE_ID
#define CURRENT_FILE_ID FID_Development_Fortify_Source_Fortify_FortifyGameMode_h


PRAGMA_ENABLE_DEPRECATION_WARNINGS
