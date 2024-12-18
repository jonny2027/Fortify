// Copyright Epic Games, Inc. All Rights Reserved.
/*===========================================================================
	Generated code exported from UnrealHeaderTool.
	DO NOT modify this manually! Edit the corresponding .h files instead!
===========================================================================*/

#include "UObject/GeneratedCppIncludes.h"
#include "Fortify/FortifyGameMode.h"
PRAGMA_DISABLE_DEPRECATION_WARNINGS
void EmptyLinkFunctionForGeneratedCodeFortifyGameMode() {}

// Begin Cross Module References
ENGINE_API UClass* Z_Construct_UClass_AGameModeBase();
FORTIFY_API UClass* Z_Construct_UClass_AFortifyGameMode();
FORTIFY_API UClass* Z_Construct_UClass_AFortifyGameMode_NoRegister();
UPackage* Z_Construct_UPackage__Script_Fortify();
// End Cross Module References

// Begin Class AFortifyGameMode
void AFortifyGameMode::StaticRegisterNativesAFortifyGameMode()
{
}
IMPLEMENT_CLASS_NO_AUTO_REGISTRATION(AFortifyGameMode);
UClass* Z_Construct_UClass_AFortifyGameMode_NoRegister()
{
	return AFortifyGameMode::StaticClass();
}
struct Z_Construct_UClass_AFortifyGameMode_Statics
{
#if WITH_METADATA
	static constexpr UECodeGen_Private::FMetaDataPairParam Class_MetaDataParams[] = {
		{ "HideCategories", "Info Rendering MovementReplication Replication Actor Input Movement Collision Rendering HLOD WorldPartition DataLayers Transformation" },
		{ "IncludePath", "FortifyGameMode.h" },
		{ "ModuleRelativePath", "FortifyGameMode.h" },
		{ "ShowCategories", "Input|MouseInput Input|TouchInput" },
	};
#endif // WITH_METADATA
	static UObject* (*const DependentSingletons[])();
	static constexpr FCppClassTypeInfoStatic StaticCppClassTypeInfo = {
		TCppClassTypeTraits<AFortifyGameMode>::IsAbstract,
	};
	static const UECodeGen_Private::FClassParams ClassParams;
};
UObject* (*const Z_Construct_UClass_AFortifyGameMode_Statics::DependentSingletons[])() = {
	(UObject* (*)())Z_Construct_UClass_AGameModeBase,
	(UObject* (*)())Z_Construct_UPackage__Script_Fortify,
};
static_assert(UE_ARRAY_COUNT(Z_Construct_UClass_AFortifyGameMode_Statics::DependentSingletons) < 16);
const UECodeGen_Private::FClassParams Z_Construct_UClass_AFortifyGameMode_Statics::ClassParams = {
	&AFortifyGameMode::StaticClass,
	"Game",
	&StaticCppClassTypeInfo,
	DependentSingletons,
	nullptr,
	nullptr,
	nullptr,
	UE_ARRAY_COUNT(DependentSingletons),
	0,
	0,
	0,
	0x008802ACu,
	METADATA_PARAMS(UE_ARRAY_COUNT(Z_Construct_UClass_AFortifyGameMode_Statics::Class_MetaDataParams), Z_Construct_UClass_AFortifyGameMode_Statics::Class_MetaDataParams)
};
UClass* Z_Construct_UClass_AFortifyGameMode()
{
	if (!Z_Registration_Info_UClass_AFortifyGameMode.OuterSingleton)
	{
		UECodeGen_Private::ConstructUClass(Z_Registration_Info_UClass_AFortifyGameMode.OuterSingleton, Z_Construct_UClass_AFortifyGameMode_Statics::ClassParams);
	}
	return Z_Registration_Info_UClass_AFortifyGameMode.OuterSingleton;
}
template<> FORTIFY_API UClass* StaticClass<AFortifyGameMode>()
{
	return AFortifyGameMode::StaticClass();
}
DEFINE_VTABLE_PTR_HELPER_CTOR(AFortifyGameMode);
AFortifyGameMode::~AFortifyGameMode() {}
// End Class AFortifyGameMode

// Begin Registration
struct Z_CompiledInDeferFile_FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_Statics
{
	static constexpr FClassRegisterCompiledInInfo ClassInfo[] = {
		{ Z_Construct_UClass_AFortifyGameMode, AFortifyGameMode::StaticClass, TEXT("AFortifyGameMode"), &Z_Registration_Info_UClass_AFortifyGameMode, CONSTRUCT_RELOAD_VERSION_INFO(FClassReloadVersionInfo, sizeof(AFortifyGameMode), 516370827U) },
	};
};
static FRegisterCompiledInInfo Z_CompiledInDeferFile_FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_2527824986(TEXT("/Script/Fortify"),
	Z_CompiledInDeferFile_FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_Statics::ClassInfo, UE_ARRAY_COUNT(Z_CompiledInDeferFile_FID_Development_Fortify_Source_Fortify_FortifyGameMode_h_Statics::ClassInfo),
	nullptr, 0,
	nullptr, 0);
// End Registration
PRAGMA_ENABLE_DEPRECATION_WARNINGS
