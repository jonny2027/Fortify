// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Engine/SkeletalMesh.h"

#include "MuR/Ptr.h"
#include "MuR/Model.h"
#include "MuR/Parameters.h"

#include "CustomizableObjectSkeletalMesh.generated.h"

namespace mu
{
	typedef uint64 FResourceID;
}

class UCustomizableObjectInstance;
class UCustomizableObject;
class FUpdateContextPrivate;

class FCustomizableObjectMeshStreamIn;

FName GenerateUniqueNameFromCOInstance(const UCustomizableObjectInstance& Instance);

/**
 * CustomizableObjectSkeletalMesh is a class inheriting from USkeletalMesh with the sole purpose of allowing skeletal meshes generated by Mutable
 * to stream-in Mesh LODs. 
 */
UCLASS()
class CUSTOMIZABLEOBJECT_API UCustomizableObjectSkeletalMesh : public USkeletalMesh
{
	GENERATED_BODY()

	friend FCustomizableObjectMeshStreamIn;

public: // Public Methods

	static UCustomizableObjectSkeletalMesh* CreateSkeletalMesh(const TSharedRef<FUpdateContextPrivate>& InOperationData, 
		const UCustomizableObjectInstance& InInstance, UCustomizableObject& CustomizableObject, const int32 InstanceComponentIndex);

private: // Private Methods

	//~ Begin UStreamableRenderAsset Interface.
	virtual bool StreamIn(int32 NewMipCount, bool bHighPrio) override;
	//~ End UStreamableRenderAsset Interface.


public: // Public Variables

private: // Private Variables
	FString CustomizableObjectPathName;
	FString InstancePathName;

	TSharedPtr<mu::Model, ESPMode::ThreadSafe> Model;
	
	/** Instance parameters at the time of creation. */
	mu::Ptr<mu::Parameters> Parameters;
	int32 State = -1;

	/** Mesh IDs per LOD */
	TArray<mu::FResourceID> MeshIDs;

};