// Copyright Epic Games, Inc. All Rights Reserved.

/*=============================================================================
	VulkanResources.h: Vulkan resource RHI definitions.
=============================================================================*/

#pragma once

#include "CoreMinimal.h"
#include "VulkanPlatform.h"
#include "VulkanConfiguration.h"
#include "VulkanState.h"
#include "VulkanUtil.h"
#include "BoundShaderStateCache.h"
#include "VulkanShaderResources.h"
#include "VulkanMemory.h"
#include "Misc/ScopeRWLock.h"
#include "IVulkanDynamicRHI.h"

class FVulkanDevice;
class FVulkanQueue;
class FVulkanCmdBuffer;
class FVulkanTexture;
class FVulkanResourceMultiBuffer;
class FVulkanLayout;
class FVulkanOcclusionQuery;
class FVulkanCommandBufferManager;
struct FRHITransientHeapAllocation;
struct FVulkanPendingBufferLock;

class FVulkanView;
class FVulkanViewableResource;
class FVulkanShaderResourceView;
class FVulkanUnorderedAccessView;

namespace VulkanRHI
{
	class FDeviceMemoryAllocation;
}

enum
{
	NUM_OCCLUSION_QUERIES_PER_POOL = 4096,

	NUM_TIMESTAMP_QUERIES_PER_POOL = 1024,
};

// Mirror GPixelFormats with format information for buffers
extern VkFormat GVulkanBufferFormat[PF_MAX];

// Converts the internal texture dimension to Vulkan view type
inline VkImageViewType UETextureDimensionToVkImageViewType(ETextureDimension Dimension)
{
	switch (Dimension)
	{
	case ETextureDimension::Texture2D: return VK_IMAGE_VIEW_TYPE_2D;
	case ETextureDimension::Texture2DArray: return VK_IMAGE_VIEW_TYPE_2D_ARRAY;
	case ETextureDimension::Texture3D: return VK_IMAGE_VIEW_TYPE_3D;
	case ETextureDimension::TextureCube: return VK_IMAGE_VIEW_TYPE_CUBE;
	case ETextureDimension::TextureCubeArray: return VK_IMAGE_VIEW_TYPE_CUBE_ARRAY;
	default: checkNoEntry(); return VK_IMAGE_VIEW_TYPE_MAX_ENUM;
	}
}

/** This represents a vertex declaration that hasn't been combined with a specific shader to create a bound shader. */
class FVulkanVertexDeclaration : public FRHIVertexDeclaration
{
public:
	FVertexDeclarationElementList Elements;
	uint32 Hash;
	uint32 HashNoStrides;

	FVulkanVertexDeclaration(const FVertexDeclarationElementList& InElements, uint32 InHash, uint32 InHashNoStrides);

	virtual bool GetInitializer(FVertexDeclarationElementList& Out) final override
	{
		Out = Elements;
		return true;
	}

	static void EmptyCache();

	virtual uint32 GetPrecachePSOHash() const final override { return HashNoStrides; }
};

struct FGfxPipelineDesc;

class FVulkanShaderModule : public FThreadSafeRefCountedObject
{
	static FVulkanDevice* Device;
	VkShaderModule ActualShaderModule;
public:
	FVulkanShaderModule(FVulkanDevice* DeviceIn, VkShaderModule ShaderModuleIn) : ActualShaderModule(ShaderModuleIn) 
	{
		check(DeviceIn && (Device == DeviceIn || !Device));
		Device = DeviceIn;
	}
	virtual ~FVulkanShaderModule();
	VkShaderModule& GetVkShaderModule() { return ActualShaderModule; }
};

class FVulkanShader : public IRefCountedObject
{
protected:

	static FCriticalSection VulkanShaderModulesMapCS;

public:
	virtual ~FVulkanShader();

	void PurgeShaderModules();

	TRefCountPtr<FVulkanShaderModule> GetOrCreateHandle();

	TRefCountPtr<FVulkanShaderModule> GetOrCreateHandle(const FVulkanLayout* Layout, uint32 LayoutHash)
	{
		FScopeLock Lock(&VulkanShaderModulesMapCS);
		TRefCountPtr<FVulkanShaderModule>* Found = ShaderModules.Find(LayoutHash);
		if (Found)
		{
			return *Found;
		}

		return CreateHandle(Layout, LayoutHash);
	}
	
	TRefCountPtr<FVulkanShaderModule> GetOrCreateHandle(const FGfxPipelineDesc& Desc, const FVulkanLayout* Layout, uint32 LayoutHash)
	{
		FScopeLock Lock(&VulkanShaderModulesMapCS);
		if (NeedsSpirvInputAttachmentPatching(Desc))
		{
			LayoutHash = HashCombine(LayoutHash, 1);
		}
		
		TRefCountPtr<FVulkanShaderModule>* Found = ShaderModules.Find(LayoutHash);
		if (Found)
		{
			return *Found;
		}

		return CreateHandle(Desc, Layout, LayoutHash);
	}

	inline const FString& GetDebugName() const
	{
		return CodeHeader.DebugName;
	}

	// Name should be pointing to "main_"
	void GetEntryPoint(ANSICHAR* Name, int32 NameLength)
	{
		FCStringAnsi::Snprintf(Name, NameLength, "main_%0.8x_%0.8x", SpirvContainer.GetSizeBytes(), CodeHeader.SpirvCRC);
	}

	FORCEINLINE const FVulkanShaderHeader& GetCodeHeader() const
	{
		return CodeHeader;
	}

	inline uint64 GetShaderKey() const
	{
		return ShaderKey;
	}

	// This provides a view of the raw spirv bytecode.
	// If it is stored compressed then the result of GetSpirvCode will contain the decompressed spirv.
	class FSpirvCode
	{
		friend class FVulkanShader;
		explicit FSpirvCode(TArray<uint32>&& UncompressedCodeIn) : UncompressedCode(MoveTemp(UncompressedCodeIn))
		{
			CodeView = UncompressedCode;
		}
		explicit FSpirvCode(TArrayView<uint32> UncompressedCodeView) : CodeView(UncompressedCodeView)	{	}
		TArrayView<uint32> CodeView;
		TArray<uint32> UncompressedCode;
	public:
		TArrayView<uint32> GetCodeView() {return CodeView;}
	};

	inline FSpirvCode GetSpirvCode()
	{
		return GetSpirvCode(SpirvContainer);
	}
	
	FSpirvCode GetPatchedSpirvCode(const FGfxPipelineDesc& Desc, const FVulkanLayout* Layout);

	TArray<FUniformBufferStaticSlot>& StaticSlots;

protected:

#if UE_BUILD_DEBUG || UE_BUILD_DEVELOPMENT
	FString							DebugEntryPoint;
#endif
	uint64							ShaderKey;

	/** External bindings for this shader. */
	FVulkanShaderHeader				CodeHeader;
	TMap<uint32, TRefCountPtr<FVulkanShaderModule>>	ShaderModules;
	const EShaderFrequency			Frequency;

protected:
	class FSpirvContainer
	{
		friend class FVulkanShader;
		TArray<uint8>	SpirvCode;
		int32 UncompressedSizeBytes = -1;
	public:
		bool IsCompressed() const {	return UncompressedSizeBytes != -1;	}
		int32 GetSizeBytes() const { return UncompressedSizeBytes >= 0 ? UncompressedSizeBytes : SpirvCode.Num(); }
		friend FArchive& operator<<(FArchive& Ar, class FVulkanShader::FSpirvContainer& SpirvContainer);
	} SpirvContainer;

	friend FArchive& operator<<(FArchive& Ar, class FVulkanShader::FSpirvContainer& SpirvContainer);
	static FSpirvCode PatchSpirvInputAttachments(FSpirvCode& SpirvCode);

	static FSpirvCode GetSpirvCode(const FSpirvContainer& Container);

protected:
	FVulkanDevice*					Device;

	TRefCountPtr<FVulkanShaderModule> CreateHandle(const FVulkanLayout* Layout, uint32 LayoutHash);
	TRefCountPtr<FVulkanShaderModule> CreateHandle(const FGfxPipelineDesc& Desc, const FVulkanLayout* Layout, uint32 LayoutHash);

	bool NeedsSpirvInputAttachmentPatching(const FGfxPipelineDesc& Desc) const;

	FVulkanShader(FVulkanDevice* InDevice, EShaderFrequency InFrequency, FVulkanShaderHeader&& InCodeHeader, FSpirvContainer&& InSpirvContainer, uint64 InShaderKey, TArray<FUniformBufferStaticSlot>& InStaticSlots);

	friend class FVulkanCommandListContext;
	friend class FVulkanPipelineStateCacheManager;
	friend class FVulkanComputeShaderState;
	friend class FVulkanComputePipeline;
	friend class FVulkanShaderFactory;
};

/** This represents a vertex shader that hasn't been combined with a specific declaration to create a bound shader. */
template<typename BaseResourceType, EShaderFrequency ShaderType>
class TVulkanBaseShader : public BaseResourceType, public FVulkanShader
{
private:
	TVulkanBaseShader(FVulkanDevice* InDevice, FShaderResourceTable&& InSRT, FVulkanShaderHeader&& InCodeHeader, FSpirvContainer&& InSpirvContainer, uint64 InShaderKey)
		: BaseResourceType()
		, FVulkanShader(InDevice, ShaderType, MoveTemp(InCodeHeader), MoveTemp(InSpirvContainer), InShaderKey, BaseResourceType::StaticSlots)
	{
		BaseResourceType::ShaderResourceTable = MoveTemp(InSRT);
	}
	friend class FVulkanShaderFactory;
public:
	enum { StaticFrequency = ShaderType };

	// IRefCountedObject interface.
	virtual uint32 AddRef() const override final
	{
		return FRHIResource::AddRef();
	}
	virtual uint32 Release() const override final
	{
		return FRHIResource::Release();
	}
	virtual uint32 GetRefCount() const override final
	{
		return FRHIResource::GetRefCount();
	}
};

typedef TVulkanBaseShader<FRHIVertexShader, SF_Vertex>				FVulkanVertexShader;
typedef TVulkanBaseShader<FRHIPixelShader, SF_Pixel>				FVulkanPixelShader;
typedef TVulkanBaseShader<FRHIComputeShader, SF_Compute>			FVulkanComputeShader;
typedef TVulkanBaseShader<FRHIGeometryShader, SF_Geometry>			FVulkanGeometryShader;
typedef TVulkanBaseShader<FRHIMeshShader, SF_Mesh>					FVulkanMeshShader;
typedef TVulkanBaseShader<FRHIAmplificationShader, SF_Amplification> FVulkanTaskShader;

class FVulkanRayTracingShader : public FRHIRayTracingShader, public FVulkanShader
{
private:
	FVulkanRayTracingShader(FVulkanDevice* InDevice, EShaderFrequency InFrequency, FShaderResourceTable&& InSRT, FVulkanShaderHeader&& InCodeHeader, FSpirvContainer&& InSpirvContainer, uint64 InShaderKey)
		: FRHIRayTracingShader(InFrequency)
		, FVulkanShader(InDevice, InFrequency, MoveTemp(InCodeHeader), MoveTemp(InSpirvContainer), InShaderKey, FRHIRayTracingShader::StaticSlots)
	{
		ShaderResourceTable = MoveTemp(InSRT);
	}

	FSpirvContainer AnyHitSpirvContainer;
	FSpirvContainer IntersectionSpirvContainer;

	friend class FVulkanShaderFactory;

public:
	static const uint32 MainModuleIdentifier = 0;
	static const uint32 ClosestHitModuleIdentifier = MainModuleIdentifier;
	static const uint32 AnyHitModuleIdentifier = 1;
	static const uint32 IntersectionModuleIdentifier = 2;

	TRefCountPtr<FVulkanShaderModule> GetOrCreateHandle(uint32 ModuleIdentifier);

	// IRefCountedObject interface.
	virtual uint32 AddRef() const override final
	{
		return FRHIResource::AddRef();
	}
	virtual uint32 Release() const override final
	{
		return FRHIResource::Release();
	}
	virtual uint32 GetRefCount() const override final
	{
		return FRHIResource::GetRefCount();
	}
};

class FVulkanShaderFactory
{
public:
	~FVulkanShaderFactory();

	template <typename ShaderType>
	ShaderType* CreateShader(TArrayView<const uint8> Code, FVulkanDevice* Device);

	template <typename ShaderType>
	ShaderType* LookupShader(uint64 ShaderKey) const
	{
		if (ShaderKey)
		{
			FRWScopeLock ScopedLock(RWLock[ShaderType::StaticFrequency], SLT_ReadOnly);
			FVulkanShader* const * FoundShaderPtr = ShaderMap[ShaderType::StaticFrequency].Find(ShaderKey);
			if (FoundShaderPtr)
			{
				return static_cast<ShaderType*>(*FoundShaderPtr);
			}
		}
		return nullptr;
	}

	template <EShaderFrequency ShaderFrequency>
	FVulkanRayTracingShader* CreateRayTracingShader(TArrayView<const uint8> Code, FVulkanDevice* Device);

	void LookupGfxShaders(const uint64 InShaderKeys[ShaderStage::NumGraphicsStages], FVulkanShader* OutShaders[ShaderStage::NumGraphicsStages]) const;

	void OnDeleteShader(const FVulkanShader& Shader);

private:
	mutable FRWLock RWLock[SF_NumFrequencies];
	TMap<uint64, FVulkanShader*> ShaderMap[SF_NumFrequencies];
};

class FVulkanBoundShaderState : public FRHIBoundShaderState
{
public:
	FVulkanBoundShaderState(
		FRHIVertexDeclaration* InVertexDeclarationRHI,
		FRHIVertexShader* InVertexShaderRHI,
		FRHIPixelShader* InPixelShaderRHI,
		FRHIGeometryShader* InGeometryShaderRHI
	);

	virtual ~FVulkanBoundShaderState();

	FORCEINLINE FVulkanVertexShader*   GetVertexShader() const { return (FVulkanVertexShader*)CacheLink.GetVertexShader(); }
	FORCEINLINE FVulkanPixelShader*    GetPixelShader() const { return (FVulkanPixelShader*)CacheLink.GetPixelShader(); }
	FORCEINLINE FVulkanMeshShader*     GetMeshShader() const { return (FVulkanMeshShader*)CacheLink.GetMeshShader(); }
	FORCEINLINE FVulkanTaskShader*     GetTaskShader() const { return (FVulkanTaskShader*)CacheLink.GetAmplificationShader(); }
	FORCEINLINE FVulkanGeometryShader* GetGeometryShader() const { return (FVulkanGeometryShader*)CacheLink.GetGeometryShader(); }

	const FVulkanShader* GetShader(ShaderStage::EStage Stage) const
	{
		switch (Stage)
		{
		case ShaderStage::Vertex:		return GetVertexShader();
		case ShaderStage::Pixel:		return GetPixelShader();
#if PLATFORM_SUPPORTS_MESH_SHADERS
		case ShaderStage::Mesh:			return GetMeshShader();
		case ShaderStage::Task:			return GetTaskShader();
#endif
#if VULKAN_SUPPORTS_GEOMETRY_SHADERS
		case ShaderStage::Geometry:	return GetGeometryShader();
#endif
		default: break;
		}
		checkf(0, TEXT("Invalid Shader Frequency %d"), (int32)Stage);
		return nullptr;
	}

private:
	FCachedBoundShaderStateLink_Threadsafe CacheLink;
};

struct FVulkanCpuReadbackBuffer
{
	VkBuffer Buffer;
	uint32 MipOffsets[MAX_TEXTURE_MIP_COUNT];
	uint32 MipSize[MAX_TEXTURE_MIP_COUNT];
};

class FVulkanView
{
public:

	struct FInvalidatedState
	{
		bool bInitialized = false;
	};

	struct FTypedBufferView
	{
		VkBufferView View      = VK_NULL_HANDLE;
		uint32       ViewId    = 0;
		bool         bVolatile = false; // Whether source buffer is volatile
	};

	struct FStructuredBufferView
	{
		VkBuffer Buffer = VK_NULL_HANDLE;
		uint32 HandleId = 0;
		uint32 Offset   = 0;
		uint32 Size     = 0;
	};

	struct FAccelerationStructureView
	{
		VkAccelerationStructureKHR Handle = VK_NULL_HANDLE;
	};

	struct FTextureView
	{
		VkImageView View   = VK_NULL_HANDLE;
		VkImage     Image  = VK_NULL_HANDLE;
		uint32      ViewId = 0;
	};

	typedef TVariant<
		  FInvalidatedState
		, FTypedBufferView
		, FTextureView
		, FStructuredBufferView
		, FAccelerationStructureView
	> TStorage;

	enum EType
	{
		Null                  = TStorage::IndexOfType<FInvalidatedState     >(),
		TypedBuffer           = TStorage::IndexOfType<FTypedBufferView      >(),
		Texture               = TStorage::IndexOfType<FTextureView          >(),
		StructuredBuffer      = TStorage::IndexOfType<FStructuredBufferView >(),
		AccelerationStructure = TStorage::IndexOfType<FAccelerationStructureView>(),
	};

	FVulkanView(FVulkanDevice& InDevice, VkDescriptorType InDescriptorType);

	~FVulkanView();

	void Invalidate();

	EType GetViewType() const
	{
		return EType(Storage.GetIndex());
	}

	bool IsInitialized() const
	{
		return (GetViewType() != Null) || Storage.Get<FInvalidatedState>().bInitialized;
	}

	FTypedBufferView           const& GetTypedBufferView          () const { return Storage.Get<FTypedBufferView          >(); }
	FTextureView               const& GetTextureView              () const { return Storage.Get<FTextureView              >(); }
	FStructuredBufferView      const& GetStructuredBufferView     () const { return Storage.Get<FStructuredBufferView     >(); }
	FAccelerationStructureView const& GetAccelerationStructureView() const { return Storage.Get<FAccelerationStructureView>(); }

	// NOTE: The InOffset applies to the FVulkanResourceMultiBuffer (it does not include any internal Allocation offsets that may exist)
	FVulkanView* InitAsTypedBufferView(
		  FVulkanResourceMultiBuffer* Buffer
		, EPixelFormat Format
		, uint32 InOffset
		, uint32 InSize);

	FVulkanView* InitAsTextureView(
		  VkImage InImage
		, VkImageViewType ViewType
		, VkImageAspectFlags AspectFlags
		, EPixelFormat UEFormat
		, VkFormat Format
		, uint32 FirstMip
		, uint32 NumMips
		, uint32 ArraySliceIndex
		, uint32 NumArraySlices
		, bool bUseIdentitySwizzle = false
		, VkImageUsageFlags ImageUsageFlags = 0
		, VkSamplerYcbcrConversion SamplerYcbcrConversion = nullptr);

	// NOTE: The InOffset applies to the FVulkanResourceMultiBuffer (it does not include any internal Allocation offsets that may exist)
	FVulkanView* InitAsStructuredBufferView(
		  FVulkanResourceMultiBuffer* Buffer
		, uint32 InOffset
		, uint32 InSize);

	FVulkanView* InitAsAccelerationStructureView(
		  FVulkanResourceMultiBuffer* Buffer
		, uint32 Offset
		, uint32 Size);

	// No moving or copying
	FVulkanView(FVulkanView     &&) = delete;
	FVulkanView(FVulkanView const&) = delete;
	FVulkanView& operator = (FVulkanView     &&) = delete;
	FVulkanView& operator = (FVulkanView const&) = delete;

	FRHIDescriptorHandle GetBindlessHandle() const
	{
		return BindlessHandle;
	}

private:
	FVulkanDevice& Device;
	FRHIDescriptorHandle BindlessHandle;
	TStorage Storage;
};

class FVulkanLinkedView : public FVulkanView, public TIntrusiveLinkedList<FVulkanLinkedView>
{
protected:
	FVulkanLinkedView(FVulkanDevice& Device, VkDescriptorType DescriptorType)
		: FVulkanView(Device, DescriptorType)
	{}

	~FVulkanLinkedView()
	{
		Unlink();
	}

public:
	virtual void UpdateView() = 0;
};

class FVulkanViewableResource
{
public:
	virtual ~FVulkanViewableResource()
	{
		checkf(!HasLinkedViews(), TEXT("All linked views must have been removed before the underlying resource can be deleted."));
	}

	bool HasLinkedViews() const
	{
		return LinkedViews != nullptr;
	}

	// @todo convert views owned by the texture into proper
	// FVulkanView instances, then remove 'virtual' from this class
	virtual void UpdateLinkedViews();

private:
	friend FVulkanShaderResourceView;
	friend FVulkanUnorderedAccessView;
	FVulkanLinkedView* LinkedViews = nullptr;
};

enum class EImageOwnerType : uint8
{
	None,
	LocalOwner,
	ExternalOwner,
	Aliased
};

class FVulkanTexture : public FRHITexture, public FVulkanEvictable, public FVulkanViewableResource
{
public:
	// Regular constructor.
	FVulkanTexture(FRHICommandListBase* RHICmdList, FVulkanDevice& InDevice, const FRHITextureCreateDesc& InCreateDesc, const FRHITransientHeapAllocation* InTransientHeapAllocation);

	FVulkanTexture(FVulkanDevice& InDevice, const FRHITextureCreateDesc& InCreateDesc, const FRHITransientHeapAllocation* InTransientHeapAllocation)
		: FVulkanTexture(nullptr, InDevice, InCreateDesc, InTransientHeapAllocation)
	{}

	// Construct from external resource.
	FVulkanTexture(FVulkanDevice& InDevice, const FRHITextureCreateDesc& InCreateDesc, VkImage InImage, const FVulkanRHIExternalImageDeleteCallbackInfo& InExternalImageDeleteCallbackInfo);

#if PLATFORM_ANDROID
	// Construct from Android Hardware buffer. HardwareBuffer_Desc could be extracted from HardwareBuffer, but adding it here makes the function signature unambigious
	FVulkanTexture(FVulkanDevice& InDevice, const FRHITextureCreateDesc& InCreateDesc, const AHardwareBuffer_Desc& HardwareBufferDesc, AHardwareBuffer* HardwareBuffer);
#endif

	// Aliasing constructor.
	FVulkanTexture(FVulkanDevice& InDevice, const FRHITextureCreateDesc& InCreateDesc, FTextureRHIRef& SrcTextureRHI);

	virtual ~FVulkanTexture();

	void AliasTextureResources(FTextureRHIRef& SrcTextureRHI);

	// View with all mips/layers
	FVulkanView* DefaultView = nullptr;
	// View with all mips/layers, but if it's a Depth/Stencil, only the Depth view
	FVulkanView* PartialView = nullptr;

	FTextureRHIRef AliasedTexture;

	virtual void OnLayoutTransition(FVulkanCommandListContext& Context, VkImageLayout NewLayout) {}

	template<typename T>
	void DumpMemory(T Callback)
	{
		const FIntVector SizeXYZ = GetSizeXYZ();
		Callback(TEXT("FVulkanTexture"), GetName(), this, static_cast<FRHIResource*>(this), SizeXYZ.X, SizeXYZ.Y, SizeXYZ.Z, StorageFormat);
	}

	// FVulkanEvictable interface.
	bool CanMove() const override { return false; }
	bool CanEvict() const override { return false; }
	void Evict(FVulkanDevice& Device, FVulkanCommandListContext& Context) override; ///evict to system memory
	void Move(FVulkanDevice& Device, FVulkanCommandListContext& Context, VulkanRHI::FVulkanAllocation& NewAllocation) override; //move to a full new allocation
	FVulkanTexture* GetEvictableTexture() override { return this; }

	bool GetTextureResourceInfo(FRHIResourceInfo& OutResourceInfo) const;

	void* GetNativeResource() const override final { return (void*)Image; }
	void* GetTextureBaseRHI() override final { return this; }

	virtual FRHIDescriptorHandle GetDefaultBindlessHandle() const override final
	{
		check(PartialView);
		return PartialView->GetBindlessHandle();
	}

	struct FImageCreateInfo
	{
		VkImageCreateInfo ImageCreateInfo;
		//only used when HasImageFormatListKHR is supported. Otherise VK_IMAGE_CREATE_MUTABLE_FORMAT_BIT is used.
		VkImageFormatListCreateInfoKHR ImageFormatListCreateInfo;
		//used when TexCreate_External is given
		VkExternalMemoryImageCreateInfoKHR ExternalMemImageCreateInfo;
		// Array of formats used for mutable formats
		TArray<VkFormat, TInlineAllocator<2>> FormatsUsed;
	};

	// Seperate method for creating VkImageCreateInfo
	static void GenerateImageCreateInfo(
		FImageCreateInfo& OutImageCreateInfo,
		FVulkanDevice& InDevice,
		const FRHITextureDesc& InDesc,
		VkFormat* OutStorageFormat = nullptr,
		VkFormat* OutViewFormat = nullptr,
		bool bForceLinearTexture = false);

	void DestroySurface();
	void InvalidateMappedMemory();
	void* GetMappedPointer();

	/**
	 * Returns how much memory is used by the surface
	 */
	inline uint32 GetMemorySize() const
	{
		return MemoryRequirements.size;
	}

	/**
	 * Returns one of the texture's mip-maps stride.
	 */
	void GetMipStride(uint32 MipIndex, uint32& Stride);

	/*
	 * Returns the memory offset to the texture's mip-map.
	 */
	void GetMipOffset(uint32 MipIndex, uint32& Offset);

	/**
	* Returns how much memory a single mip uses.
	*/
	void GetMipSize(uint32 MipIndex, uint32& MipBytes);

	inline VkImageViewType GetViewType() const
	{
		return UETextureDimensionToVkImageViewType(GetDesc().Dimension);
	}

	inline VkImageTiling GetTiling() const { return Tiling; }

	inline uint32 GetNumberOfArrayLevels() const
	{
		switch (GetViewType())
		{
		case VK_IMAGE_VIEW_TYPE_1D:
		case VK_IMAGE_VIEW_TYPE_2D:
		case VK_IMAGE_VIEW_TYPE_3D:
			return 1;
		case VK_IMAGE_VIEW_TYPE_2D_ARRAY:
			return GetDesc().ArraySize;
		case VK_IMAGE_VIEW_TYPE_CUBE:
			return 6;
		case VK_IMAGE_VIEW_TYPE_CUBE_ARRAY:
			return 6 * GetDesc().ArraySize;
		default:
			ErrorInvalidViewType();
			return 1;
		}
	}
	VULKANRHI_API void ErrorInvalidViewType() const;

	// Full includes Depth+Stencil
	inline VkImageAspectFlags GetFullAspectMask() const
	{
		return FullAspectMask;
	}

	// Only Depth or Stencil
	inline VkImageAspectFlags GetPartialAspectMask() const
	{
		return PartialAspectMask;
	}

	inline bool IsDepthOrStencilAspect() const
	{
		return (FullAspectMask & (VK_IMAGE_ASPECT_DEPTH_BIT | VK_IMAGE_ASPECT_STENCIL_BIT)) != 0;
	}

	inline bool IsImageOwner() const
	{
		return (ImageOwnerType == EImageOwnerType::LocalOwner);
	}

	inline bool SupportsSampling() const
	{
		return EnumHasAllFlags(GPixelFormats[GetDesc().Format].Capabilities, EPixelFormatCapabilities::TextureSample) &&
			((ImageUsageFlags & VK_IMAGE_USAGE_SAMPLED_BIT) != 0);
	}

	inline VkImageLayout GetDefaultLayout() const
	{
		return DefaultLayout;
	}

	VULKANRHI_API VkDeviceMemory GetAllocationHandle() const;
	VULKANRHI_API uint64 GetAllocationOffset() const;

	static void InternalLockWrite(FVulkanCommandListContext& Context, FVulkanTexture* Surface, const VkBufferImageCopy& Region, VulkanRHI::FStagingBuffer* StagingBuffer);

	const FVulkanCpuReadbackBuffer* GetCpuReadbackBuffer() const { return CpuReadbackBuffer; }

	virtual void UpdateLinkedViews() override;

	FVulkanDevice* Device;
	VkImage Image;
	VkImageUsageFlags ImageUsageFlags;
	VkFormat StorageFormat;  // Removes SRGB if requested, used to upload data
	VkFormat ViewFormat;  // Format for SRVs, render targets
	VkMemoryPropertyFlags MemProps;
	VkMemoryRequirements MemoryRequirements;

	FVulkanRHIExternalImageDeleteCallbackInfo ExternalImageDeleteCallbackInfo;

private:
	void SetInitialImageState(FVulkanCommandListContext& Context, VkImageLayout InitialLayout, bool bClear, const FClearValueBinding& ClearValueBinding, bool bIsTransientResource);
	void InternalMoveSurface(FVulkanDevice& InDevice, FVulkanCommandListContext& Context, VulkanRHI::FVulkanAllocation& DestAllocation, VkImageLayout OriginalLayout);

	VkImageTiling Tiling;
	VulkanRHI::FVulkanAllocation Allocation;
	VkImageAspectFlags FullAspectMask;
	VkImageAspectFlags PartialAspectMask;
	FVulkanCpuReadbackBuffer* CpuReadbackBuffer;
	VkImageLayout DefaultLayout;

	friend struct FRHICommandSetInitialImageState;

protected:
	EImageOwnerType ImageOwnerType;
};

class FVulkanQueryPool : public VulkanRHI::FDeviceChild
{
public:
	FVulkanQueryPool(FVulkanDevice* InDevice, FVulkanCommandBufferManager* CommandBufferManager, uint32 InMaxQueries, VkQueryType InQueryType, bool bInShouldAddReset = true);
	virtual ~FVulkanQueryPool();

	inline uint32 GetMaxQueries() const
	{
		return MaxQueries;
	}

	inline VkQueryPool GetHandle() const
	{
		return QueryPool;
	}

	inline uint64 GetResultValue(uint32 Index) const
	{
		return QueryOutput[Index];
	}

protected:
	VkQueryPool QueryPool;
	VkEvent ResetEvent;
	const uint32 MaxQueries;
	const VkQueryType QueryType;
	TArray<uint64> QueryOutput;
};

class FVulkanOcclusionQueryPool : public FVulkanQueryPool
{
public:
	FVulkanOcclusionQueryPool(FVulkanDevice* InDevice, FVulkanCommandBufferManager* CommandBufferManager, uint32 InMaxQueries)
		: FVulkanQueryPool(InDevice, CommandBufferManager, InMaxQueries, VK_QUERY_TYPE_OCCLUSION)
	{
		AcquiredIndices.AddZeroed(Align(InMaxQueries, 64) / 64);
		AllocatedQueries.AddZeroed(InMaxQueries);
	}

	inline uint32 AcquireIndex(FVulkanOcclusionQuery* Query)
	{
		check(NumUsedQueries < MaxQueries);
		const uint32 Index = NumUsedQueries;
		const uint32 Word = Index / 64;
		const uint32 Bit = Index % 64;
		const uint64 Mask = (uint64)1 << (uint64)Bit;
		const uint64& WordValue = AcquiredIndices[Word];
		AcquiredIndices[Word] = WordValue | Mask;
		++NumUsedQueries;
		ensure(AllocatedQueries[Index] == nullptr);
		AllocatedQueries[Index] = Query;
		return Index;
	}

	inline void ReleaseIndex(uint32 Index)
	{
		check(Index < NumUsedQueries);
		const uint32 Word = Index / 64;
		const uint32 Bit = Index % 64;
		const uint64 Mask = (uint64)1 << (uint64)Bit;
		const uint64& WordValue = AcquiredIndices[Word];
		ensure((WordValue & Mask) == Mask);
		AcquiredIndices[Word] = WordValue & (~Mask);
		AllocatedQueries[Index] = nullptr;
	}

	inline void EndBatch(FVulkanCmdBuffer* InCmdBuffer)
	{
		ensure(State == EState::RHIT_PostBeginBatch);
		State = EState::RHIT_PostEndBatch;
		SetFence(InCmdBuffer);
	}

	bool CanBeReused();

	inline bool TryGetResults(bool bWait)
	{
		if (State == RT_PostGetResults)
		{
			return true;
		}

		if (State == RHIT_PostEndBatch)
		{
			return InternalTryGetResults(bWait);
		}

		return false;
	}

	void Reset(FVulkanCmdBuffer* InCmdBuffer, uint32 InFrameNumber);

	bool IsStalePool() const;

	void FlushAllocatedQueries();

	enum EState
	{
		Undefined,
		RHIT_PostBeginBatch,
		RHIT_PostEndBatch,
		RT_PostGetResults,
	};
	EState State = Undefined;
	
	// frame number when pool was placed into free list
	uint32 FreedFrameNumber = UINT32_MAX;
protected:
	uint32 NumUsedQueries = 0;
	TArray<FVulkanOcclusionQuery*> AllocatedQueries;
	TArray<uint64> AcquiredIndices;
	bool InternalTryGetResults(bool bWait);
	void SetFence(FVulkanCmdBuffer* InCmdBuffer);

	FVulkanCmdBuffer* CmdBuffer = nullptr;
	uint64 FenceCounter = UINT64_MAX;
	uint32 FrameNumber = UINT32_MAX;
};

class FVulkanTimingQueryPool : public FVulkanQueryPool
{
public:
	FVulkanTimingQueryPool(FVulkanDevice* InDevice, FVulkanCommandBufferManager* CommandBufferManager, uint32 InBufferSize)
		: FVulkanQueryPool(InDevice, CommandBufferManager, InBufferSize * 2, VK_QUERY_TYPE_TIMESTAMP, false)
		, BufferSize(InBufferSize)
	{
		TimestampListHandles.AddZeroed(InBufferSize * 2);
	}

	uint32 CurrentTimestamp = 0;
	uint32 NumIssuedTimestamps = 0;
	const uint32 BufferSize;

	struct FCmdBufferFence
	{
		FVulkanCmdBuffer* CmdBuffer;
		uint64 FenceCounter;
		uint64 FrameCount = UINT64_MAX;
		uint32 Attempts = 0;
	};
	TArray<FCmdBufferFence> TimestampListHandles;

	VulkanRHI::FStagingBuffer* ResultsBuffer = nullptr;
	uint64* MappedPointer = nullptr;
};

class FVulkanRenderQuery : public FRHIRenderQuery
{
public:
	FVulkanRenderQuery(ERenderQueryType InType)
		: QueryType(InType)
	{
	}

	virtual ~FVulkanRenderQuery() {}

	const ERenderQueryType QueryType;
	uint64 Result = 0;

	uint32 IndexInPool = UINT32_MAX;
};

class FVulkanOcclusionQuery : public FVulkanRenderQuery
{
public:
	FVulkanOcclusionQuery();
	virtual ~FVulkanOcclusionQuery();

	enum class EState
	{
		Undefined,
		RHI_PostBegin,
		RHI_PostEnd,
		RT_GotResults,
		FlushedFromPoolHadResults,
	};

	FVulkanOcclusionQueryPool* Pool = nullptr;

	void ReleaseFromPool();

	EState State = EState::Undefined;
};

class FVulkanTimingQuery : public FVulkanRenderQuery
{
public:
	FVulkanTimingQuery();
	virtual ~FVulkanTimingQuery();

	FVulkanTimingQueryPool* Pool = nullptr;
};

class FVulkanResourceMultiBuffer : public FRHIBuffer, public VulkanRHI::FDeviceChild, public FVulkanViewableResource
{
public:
	FVulkanResourceMultiBuffer(FVulkanDevice* InDevice, FRHIBufferDesc const& InBufferDesc, FRHIResourceCreateInfo& CreateInfo, class FRHICommandListBase* InRHICmdList = nullptr, const FRHITransientHeapAllocation* InTransientHeapAllocation = nullptr);
	virtual ~FVulkanResourceMultiBuffer();

	inline const VulkanRHI::FVulkanAllocation& GetCurrentAllocation() const
	{
		return CurrentBufferAlloc.Alloc;
	}

	inline VkBuffer GetHandle() const
	{
		return (VkBuffer)GetCurrentAllocation().VulkanHandle;
	}

	inline bool IsVolatile() const
	{
		return EnumHasAnyFlags(GetUsage(), BUF_Volatile);
	}

	// Offset used for Binding a VkBuffer
	inline uint32 GetOffset() const
	{
		return GetCurrentAllocation().Offset;
	}

	// Remaining size from the current offset
	inline uint64 GetCurrentSize() const
	{
		return GetCurrentAllocation().Size;
	}

	inline VkDeviceAddress GetDeviceAddress() const
	{
		return CurrentBufferAlloc.DeviceAddress;
	}

	inline VkBufferUsageFlags GetBufferUsageFlags() const
	{
		return BufferUsageFlags;
	}

	inline VkIndexType GetIndexType() const
	{
		return (GetStride() == 4)? VK_INDEX_TYPE_UINT32 : VK_INDEX_TYPE_UINT16;
	}

	void* Lock(FRHICommandListBase& RHICmdList, EResourceLockMode LockMode, uint32 Size, uint32 Offset);
	void Unlock(FRHICommandListBase& RHICmdList);

	void TakeOwnership(FVulkanResourceMultiBuffer& Other);
	void ReleaseOwnership();

	template<typename T>
	void DumpMemory(T Callback)
	{
		Callback(TEXT("FVulkanResourceMultiBuffer"), FName(), this, 0, GetCurrentSize(), 1, 1, VK_FORMAT_UNDEFINED);
	}

	static VkBufferUsageFlags UEToVKBufferUsageFlags(FVulkanDevice* InDevice, EBufferUsageFlags InUEUsage, bool bZeroSize);

	struct FBufferAlloc
	{
		VulkanRHI::FVulkanAllocation Alloc;
		void* HostPtr = nullptr;
		VkDeviceAddress DeviceAddress = 0;
	};

protected:

	// Will return a new allocation that can be used for this buffer (proper memory type and size)
	void AllocateMemory(FBufferAlloc& OutAlloc);

	VkBufferUsageFlags BufferUsageFlags;

	enum class ELockStatus : uint8
	{
		Unlocked,
		Locked,
		PersistentMapping,
	} LockStatus = ELockStatus::Unlocked;

	FBufferAlloc CurrentBufferAlloc;
	uint32 LockCounter = 0;

	friend class FVulkanCommandListContext;
	friend struct FRHICommandMultiBufferUnlock;
};

class FVulkanUniformBuffer : public FRHIUniformBuffer
{
public:
	FVulkanUniformBuffer(FVulkanDevice& Device, const FRHIUniformBufferLayout* InLayout, const void* Contents, EUniformBufferUsage InUsage, EUniformBufferValidation Validation);
	virtual ~FVulkanUniformBuffer();

	const TArray<TRefCountPtr<FRHIResource>>& GetResourceTable() const { return ResourceTable; }

	void UpdateResourceTable(const FRHIUniformBufferLayout& InLayout, const void* Contents, int32 ResourceNum);
	void UpdateResourceTable(FRHIResource** Resources, int32 ResourceNum);

	inline VkBuffer GetBufferHandle() const
	{
		return Allocation.GetBufferHandle();
	}

	inline uint32 GetOffset() const
	{
		return Allocation.Offset;
	}

	inline void UpdateAllocation(VulkanRHI::FVulkanAllocation& NewAlloc)
	{
		NewAlloc.Swap(Allocation);
	}

	inline bool IsUniformView() const
	{
		return UniformViewSRV != nullptr;
	}
	
	FRHIDescriptorHandle GetBindlessHandle();
	VkDeviceAddress GetDeviceAddress() const;

	void SetupUniformBufferView();

public:
	FVulkanDevice* Device;
	VulkanRHI::FVulkanAllocation Allocation;
	EUniformBufferUsage Usage;

	FRHIDescriptorHandle BindlessHandle;
	VkDeviceAddress CachedDeviceAddress = 0;
	FRHIShaderResourceView* UniformViewSRV;
};

class FVulkanUnorderedAccessView final : public FRHIUnorderedAccessView, public FVulkanLinkedView
{
public:
	FVulkanUnorderedAccessView(FRHICommandListBase& RHICmdList, FVulkanDevice& InDevice, FRHIViewableResource* InResource, FRHIViewDesc const& InViewDesc);

	FVulkanViewableResource* GetBaseResource() const;
	void UpdateView() override;

	virtual FRHIDescriptorHandle GetBindlessHandle() const override
	{
		return FVulkanLinkedView::GetBindlessHandle();
	}

	void Clear(TRHICommandList_RecursiveHazardous<FVulkanCommandListContext>& RHICmdList, const void* ClearValue, bool bFloat);
};


class FVulkanShaderResourceView final : public FRHIShaderResourceView, public FVulkanLinkedView
{
public:
	FVulkanShaderResourceView(FRHICommandListBase& RHICmdList, FVulkanDevice& InDevice, FRHIViewableResource* InResource, FRHIViewDesc const& InViewDesc);

	FVulkanViewableResource* GetBaseResource() const;
	void UpdateView() override;

	virtual FRHIDescriptorHandle GetBindlessHandle() const override
	{
		return FVulkanLinkedView::GetBindlessHandle();
	}
};

class FVulkanVertexInputStateInfo
{
public:
	FVulkanVertexInputStateInfo();
	~FVulkanVertexInputStateInfo();

	void Generate(FVulkanVertexDeclaration* VertexDeclaration, uint32 VertexHeaderInOutAttributeMask);

	inline uint32 GetHash() const
	{
		check(Info.sType == VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO);
		return Hash;
	}

	inline const VkPipelineVertexInputStateCreateInfo& GetInfo() const
	{
		return Info;
	}

	bool operator ==(const FVulkanVertexInputStateInfo& Other);

protected:
	VkPipelineVertexInputStateCreateInfo Info;
	uint32 Hash;

	uint32 BindingsNum;
	uint32 BindingsMask;

	//#todo-rco: Remove these TMaps
	TMap<uint32, uint32> BindingToStream;
	TMap<uint32, uint32> StreamToBinding;
	VkVertexInputBindingDescription Bindings[MaxVertexElementCount];

	uint32 AttributesNum;
	VkVertexInputAttributeDescription Attributes[MaxVertexElementCount];

	friend class FVulkanPendingGfxState;
	friend class FVulkanPipelineStateCacheManager;
};

// This class holds the staging area for packed global uniform buffers for a given shader
class FPackedUniformBuffers
{
public:
	// One buffer is a chunk of bytes
	typedef TArray<uint8> FPackedBuffer;

	void Init(const FVulkanShaderHeader& InCodeHeader, uint32& OutPackedUniformBufferStagingMask)
	{
		if (InCodeHeader.PackedGlobalsSize > 0)
		{
			check(PackedUniformBuffers.Num() == 0);
			PackedUniformBuffers.AddUninitialized(InCodeHeader.PackedGlobalsSize);
			OutPackedUniformBufferStagingMask = 1;
		}
		else
		{
			OutPackedUniformBufferStagingMask = 0;
		}
	}

	inline void SetPackedGlobalParameter(uint32 ByteOffset, uint32 NumBytes, const void* RESTRICT NewValue, uint32& InOutPackedUniformBufferStagingDirty)
	{
		check(ByteOffset + NumBytes <= (uint32)PackedUniformBuffers.Num());
		check((NumBytes & 3) == 0 && (ByteOffset & 3) == 0);
		uint32* RESTRICT RawDst = (uint32*)(PackedUniformBuffers.GetData() + ByteOffset);
		uint32* RESTRICT RawSrc = (uint32*)NewValue;
		uint32* RESTRICT RawSrcEnd = RawSrc + (NumBytes >> 2);

		bool bChanged = false;
		while (RawSrc != RawSrcEnd)
		{
			bChanged |= CopyAndReturnNotEqual(*RawDst++, *RawSrc++);
		}

		if (bChanged)
		{
			InOutPackedUniformBufferStagingDirty = 1;
		}
	}

	inline const FPackedBuffer& GetBuffer() const
	{
		return PackedUniformBuffers;
	}

protected:
	FPackedBuffer PackedUniformBuffers;
};

class FVulkanStagingBuffer : public FRHIStagingBuffer
{
	friend class FVulkanCommandListContext;
public:
	FVulkanStagingBuffer()
		: FRHIStagingBuffer()
	{
		check(!bIsLocked);
	}

	virtual ~FVulkanStagingBuffer();

	virtual void* Lock(uint32 Offset, uint32 NumBytes) final override;
	virtual void Unlock() final override;

private:
	VulkanRHI::FStagingBuffer* StagingBuffer = nullptr;
	uint32 QueuedNumBytes = 0;
	// The staging buffer was allocated from this device.
	FVulkanDevice* Device;
};

class FVulkanGPUFence : public FRHIGPUFence
{
public:
	FVulkanGPUFence(FName InName)
		: FRHIGPUFence(InName)
	{
	}

	virtual void Clear() final override;
	virtual bool Poll() const final override;

	FVulkanCmdBuffer* GetCmdBuffer() const { return CmdBuffer; }

protected:
	FVulkanCmdBuffer*	CmdBuffer = nullptr;
	uint64				FenceSignaledCounter = MAX_uint64;

	friend class FVulkanCommandListContext;
};

template<class T>
struct TVulkanResourceTraits
{
};
template<>
struct TVulkanResourceTraits<FRHIVertexDeclaration>
{
	typedef FVulkanVertexDeclaration TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIVertexShader>
{
	typedef FVulkanVertexShader TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIMeshShader>
{
	typedef FVulkanMeshShader TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIAmplificationShader>
{
	typedef FVulkanTaskShader TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIGeometryShader>
{
	typedef FVulkanGeometryShader TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIPixelShader>
{
	typedef FVulkanPixelShader TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIComputeShader>
{
	typedef FVulkanComputeShader TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIRenderQuery>
{
	typedef FVulkanRenderQuery TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIUniformBuffer>
{
	typedef FVulkanUniformBuffer TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIBuffer>
{
	typedef FVulkanResourceMultiBuffer TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIShaderResourceView>
{
	typedef FVulkanShaderResourceView TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIUnorderedAccessView>
{
	typedef FVulkanUnorderedAccessView TConcreteType;
};

template<>
struct TVulkanResourceTraits<FRHISamplerState>
{
	typedef FVulkanSamplerState TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIRasterizerState>
{
	typedef FVulkanRasterizerState TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIDepthStencilState>
{
	typedef FVulkanDepthStencilState TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIBlendState>
{
	typedef FVulkanBlendState TConcreteType;
};

template<>
struct TVulkanResourceTraits<FRHIBoundShaderState>
{
	typedef FVulkanBoundShaderState TConcreteType;
};

template<>
struct TVulkanResourceTraits<FRHIStagingBuffer>
{
	typedef FVulkanStagingBuffer TConcreteType;
};

template<>
struct TVulkanResourceTraits<FRHIGPUFence>
{
	typedef FVulkanGPUFence TConcreteType;
};

template<typename TRHIType>
static FORCEINLINE typename TVulkanResourceTraits<TRHIType>::TConcreteType* ResourceCast(TRHIType* Resource)
{
	return static_cast<typename TVulkanResourceTraits<TRHIType>::TConcreteType*>(Resource);
}

template<typename TRHIType>
static FORCEINLINE const typename TVulkanResourceTraits<TRHIType>::TConcreteType* ResourceCast(const TRHIType* Resource)
{
	return static_cast<const typename TVulkanResourceTraits<TRHIType>::TConcreteType*>(Resource);
}

static FORCEINLINE FVulkanTexture* ResourceCast(FRHITexture* Texture)
{
	return static_cast<FVulkanTexture*>(Texture->GetTextureBaseRHI());
}

class FVulkanRayTracingScene;
class FVulkanRayTracingGeometry;
class FVulkanRayTracingShaderTable;
class FVulkanRayTracingPipelineState;
template<>
struct TVulkanResourceTraits<FRHIRayTracingScene>
{
	typedef FVulkanRayTracingScene TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIRayTracingGeometry>
{
	typedef FVulkanRayTracingGeometry TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIShaderBindingTable>
{
	typedef FVulkanRayTracingShaderTable TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIRayTracingPipelineState>
{
	typedef FVulkanRayTracingPipelineState TConcreteType;
};
template<>
struct TVulkanResourceTraits<FRHIRayTracingShader>
{
	typedef FVulkanRayTracingShader TConcreteType;
};