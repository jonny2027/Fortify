// Copyright 2024, Jonathan Ogle-Barrington

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "FortifyWebSocketConnection.generated.h"

DECLARE_DYNAMIC_DELEGATE_FourParams(FWebSocketDisconnectDelegate, int32, ConnectionId, int32, StatusCode, const FString&, Reason, bool, bWasClean);
DECLARE_DYNAMIC_DELEGATE_TwoParams(FWebSocketErrorDelegate, int32, ConnectionId, const FString&, ErrorTxt);
DECLARE_DYNAMIC_DELEGATE_OneParam(FWebSocketConnectDelegate, int32, ConnectionId);
DECLARE_DYNAMIC_DELEGATE_TwoParams(FWebSocketReceivedRawMessageDelegate, int32, ConnectionId, UPARAM(ref) TArray<uint8>&, Message);
DECLARE_DYNAMIC_DELEGATE_TwoParams(FWebSocketReceivedTextMessageDelegate, int32, ConnectionId, const FString&, Message);

UCLASS(Blueprintable, BlueprintType)
class AFortifyWebSocketConnection : public AActor
{
	GENERATED_BODY()
	
public:
	
	AFortifyWebSocketConnection();

protected:

	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

public:	

	virtual void Tick(float DeltaTime) override;
	
	UFUNCTION(BlueprintCallable, Category = "Socket")
	void Connect(const FString& Protocol,
		const FString& ServerAddress,
		int32 Port,
		const FString& Path,
		const FWebSocketDisconnectDelegate& OnDisconnected, 
		const FWebSocketErrorDelegate& OnError,
		const FWebSocketConnectDelegate& OnConnected,
		const FWebSocketReceivedRawMessageDelegate& OnRawMessageReceived,
		const FWebSocketReceivedTextMessageDelegate& OnTextMessageReceived,
		int32& ConnectionId);
	
	UFUNCTION(BlueprintCallable, Category = "Socket")
	void Disconnect(int32 ConnectionId);
	
	UFUNCTION(BlueprintCallable, Category = "Socket")
	bool SendData(int32 ConnectionId, TArray<uint8> DataToSend);
	
	UFUNCTION(BlueprintCallable, Category = "Socket")
	bool SendText(int32 ConnectionId, const FString& TextMessage);

	UFUNCTION(BlueprintPure, meta = (DisplayName = "Append Bytes", CommutativeAssociativeBinaryOperator = "true"), Category = "Socket")
	static TArray<uint8> ConcatenateBytesBytes(TArray<uint8> A, TArray<uint8> B);

	UFUNCTION(BlueprintPure, meta = (DisplayName = "Int To Bytes", CompactNodeTitle = "->", Keywords = "cast convert", BlueprintAutocast), Category = "Socket")
	static TArray<uint8> ConvertIntToBytes(int32 InInt);

	UFUNCTION(BlueprintPure, meta = (DisplayName = "String To Bytes", CompactNodeTitle = "->", Keywords = "cast convert", BlueprintAutocast), Category = "Socket")
	static TArray<uint8> ConvertStringToBytes(const FString& InStr);
	
	UFUNCTION(BlueprintPure, meta = (DisplayName = "Float To Bytes", CompactNodeTitle = "->", Keywords = "cast convert", BlueprintAutocast), Category = "Socket")
	static TArray<uint8> ConvertFloatToBytes(float InFloat);
	
	UFUNCTION(BlueprintPure, meta = (DisplayName = "Byte To Bytes", CompactNodeTitle = "->", Keywords = "cast convert", BlueprintAutocast), Category = "Socket")
	static TArray<uint8> ConvertByteToBytes(uint8 InByte);

	UFUNCTION(BlueprintCallable, meta = (DisplayName = "Read Int", Keywords = "read int"), Category = "Socket")
	static int32 MessageReadInt(UPARAM(ref) TArray<uint8>& Message);

	UFUNCTION(BlueprintCallable, meta = (DisplayName = "Read Byte", Keywords = "read byte int8 uint8"), Category = "Socket")
	static uint8 MessageReadByte(UPARAM(ref) TArray<uint8>& Message);

	UFUNCTION(BlueprintCallable, meta = (DisplayName = "Read Bytes", Keywords = "read bytes"), Category = "Socket")
	static bool MessageReadBytes(int32 NumBytes, UPARAM(ref) TArray<uint8>& Message, TArray<uint8>& ReturnArray);

	UFUNCTION(BlueprintCallable, meta = (DisplayName = "Read Float", Keywords = "read float"), Category = "Socket")
	static float MessageReadFloat(UPARAM(ref) TArray<uint8>& Message);

	UFUNCTION(BlueprintCallable, meta = (DisplayName = "Read String", Keywords = "read string"), Category = "Socket")
	static FString MessageReadString(UPARAM(ref) TArray<uint8>& Message, int32 StringLength);

	UFUNCTION(BlueprintCallable, BlueprintPure, Category = "Socket")
	bool IsConnected(int32 ConnectionId);

	static void PrintToConsole(FString Str, bool Error);
	
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Socket")
	int32 SendBufferSize = 16384;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Socket")
	int32 ReceiveBufferSize = 16384;

private:
	TMap<int32, TSharedRef<class IWebSocket>> WebSockets;
	TMap<int32, TArray<uint8>> ReceiveBuffers;
	
	int32 NextConnectionId = 0;
	static bool bPostErrorsToMessageLog;
};