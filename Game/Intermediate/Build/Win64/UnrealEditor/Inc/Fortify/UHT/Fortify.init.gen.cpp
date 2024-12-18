// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeFortify_init() {}
	static FPackageRegistrationInfo Z_Registration_Info_UPackage__Script_Fortify;
	FORCENOINLINE UPackage* Z_Construct_UPackage__Script_Fortify()
	{
		if (!Z_Registration_Info_UPackage__Script_Fortify.OuterSingleton)
		{
			static const UECodeGen_Private::FPackageParams PackageParams = {
				"/Script/Fortify",
				nullptr,
				0,
				PKG_CompiledIn | 0x00000000,
				0x577B63D4,
				0x5837B005,
				METADATA_PARAMS(0, nullptr)
			};
			UECodeGen_Private::ConstructUPackage(Z_Registration_Info_UPackage__Script_Fortify.OuterSingleton, PackageParams);
		}
		return Z_Registration_Info_UPackage__Script_Fortify.OuterSingleton;
	}
	static FRegisterCompiledInInfo Z_CompiledInDeferPackage_UPackage__Script_Fortify(Z_Construct_UPackage__Script_Fortify, TEXT("/Script/Fortify"), Z_Registration_Info_UPackage__Script_Fortify, CONSTRUCT_RELOAD_VERSION_INFO(FPackageReloadVersionInfo, 0x577B63D4, 0x5837B005));
PRAGMA_ENABLE_DEPRECATION_WARNINGS
