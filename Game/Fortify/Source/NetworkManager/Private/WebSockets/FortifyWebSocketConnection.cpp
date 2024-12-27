// Copyright 2024, Jonathan Ogle-Barrington

#include "WebSockets/FortifyWebSocketConnection.h"
#include <string>
#include "Logging/MessageLog.h"
#include "HAL/UnrealMemory.h"
#include "WebSocketsModule.h"
#include "IWebSocket.h"
#include "UObject/WeakObjectPtrTemplates.h"

bool AFortifyWebSocketConnection::bPostErrorsToMessageLog = false;

AFortifyWebSocketConnection::AFortifyWebSocketConnection()
{
	PrimaryActorTick.bCanEverTick = true;
}

void AFortifyWebSocketConnection::BeginPlay()
{
	Super::BeginPlay();	
}

void AFortifyWebSocketConnection::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	Super::EndPlay(EndPlayReason);
	TArray<int32> keys;
	WebSockets.GetKeys(keys);

	for (auto &key : keys)
	{
		Disconnect(key);
	}
}

void AFortifyWebSocketConnection::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);
}

void AFortifyWebSocketConnection::Connect(const FString& Protocol,
	const FString& ServerAddress,
	int32 Port,
	const FString& Path,
	const FWebSocketDisconnectDelegate& OnDisconnected,
	const FWebSocketErrorDelegate& OnError,
	const FWebSocketConnectDelegate& OnConnected,
	const FWebSocketReceivedRawMessageDelegate& OnRawMessageReceived,
	const FWebSocketReceivedTextMessageDelegate& OnTextMessageReceived,
	int32& ConnectionId)
{
	ConnectionId = NextConnectionId;
	NextConnectionId++;

	//const FString ServerURL = TEXT("ws://localhost:3000/mmo");
	const FString ServerURL = Protocol + TEXT("://") + ServerAddress + TEXT(":") + FString::FromInt(Port) + TEXT("/") + Path;

	TSharedRef<IWebSocket> Socket = FWebSocketsModule::Get().CreateWebSocket(ServerURL, Protocol);
	WebSockets.Add(ConnectionId, Socket);
	TArray<uint8> emptyArray;
	ReceiveBuffers.Add(ConnectionId, emptyArray);

	TWeakObjectPtr<AFortifyWebSocketConnection> thisActor = TWeakObjectPtr<AFortifyWebSocketConnection>(this);

	Socket->OnConnected().AddLambda([thisActor, ConnectionId, OnConnected]()
	{
		if (!thisActor.IsValid())
		{
			return;
		}
		
		UE_LOG(LogTemp, Log, TEXT("Connected to WebSocket server."));
		
		OnConnected.ExecuteIfBound(ConnectionId);
	});

	Socket->OnConnectionError().AddLambda([thisActor, ConnectionId, OnError](const FString& ErrorText)
	{
		if (!thisActor.IsValid())
		{
			return;
		}

		UE_LOG(LogTemp, Log, TEXT("Failed to connect to websocket server with error: \"%s\"."), *ErrorText);
		
		OnError.ExecuteIfBound(ConnectionId, ErrorText);
		thisActor.Get()->WebSockets.Remove(ConnectionId);
		thisActor.Get()->ReceiveBuffers.Remove(ConnectionId);
	});
	
	Socket->OnRawMessage().AddLambda([thisActor, ConnectionId, this, OnRawMessageReceived](const void* Data, SIZE_T Length, SIZE_T BytesRemaining)
	{
		if (!thisActor.IsValid())
		{
			return;
		}
		
		if (BytesRemaining == 0 && ReceiveBuffers[ConnectionId].Num() == 0)
		{
			TArray<uint8> frameData(static_cast<const uint8*>(Data), Length);
			OnRawMessageReceived.ExecuteIfBound(ConnectionId, frameData);
		}
		else
		{
			ReceiveBuffers[ConnectionId].Append(static_cast<const uint8*>(Data), Length);
			if (BytesRemaining == 0)
			{
				OnRawMessageReceived.ExecuteIfBound(ConnectionId, ReceiveBuffers[ConnectionId]);
				ReceiveBuffers[ConnectionId].Empty();
			}
		}
	});

	Socket->OnMessage().AddLambda([thisActor, ConnectionId, OnTextMessageReceived](const FString& Message)
	{
		if (!thisActor.IsValid())
		{
			return;
		}
		
		OnTextMessageReceived.ExecuteIfBound(ConnectionId, Message);
	});

	Socket->OnClosed().AddLambda([thisActor, ConnectionId, OnDisconnected](int32 StatusCode, const FString& Reason, bool bWasClean)
	{
		if (!thisActor.IsValid())
		{
			return;
		}
		
		UE_LOG(LogTemp, Log, TEXT("Connection to WebSocket server has been closed with status code: \"%d\" and reason: \"%s\"."), StatusCode, *Reason);

		OnDisconnected.ExecuteIfBound(ConnectionId, StatusCode, Reason, bWasClean);
		thisActor.Get()->WebSockets.Remove(ConnectionId);
		thisActor.Get()->ReceiveBuffers.Remove(ConnectionId);
	});

	Socket->Connect();
}

void AFortifyWebSocketConnection::Disconnect(int32 ConnectionId)
{	
	auto ws = WebSockets.Find(ConnectionId);
	if (ws && ws->Get().IsConnected())
	{
		ws->Get().Close(1000, FString("Successful operation / regular socket shutdown"));
	}
}

bool AFortifyWebSocketConnection::SendData(int32 ConnectionId /*= 0*/, TArray<uint8> DataToSend)
{
	if (WebSockets.Contains(ConnectionId))
	{
		if (WebSockets[ConnectionId]->IsConnected())
		{
			WebSockets[ConnectionId]->Send(DataToSend.GetData(), DataToSend.Num(), true);
			return true;
		}
		else
		{
			UE_LOG(LogTemp, Warning, TEXT("Log: WebSocket %d isn't connected"), ConnectionId);
		}
	}
	else
	{
		UE_LOG(LogTemp, Log, TEXT("Log: WebSocketId %d doesn't exist"), ConnectionId);
	}
	return false;
}

bool AFortifyWebSocketConnection::SendText(int32 ConnectionId /*= 0*/, const FString& TextMessage)
{
	if (WebSockets.Contains(ConnectionId))
	{
		if (WebSockets[ConnectionId]->IsConnected())
		{
			WebSockets[ConnectionId]->Send(TextMessage);
			return true;
		}
		else
		{
			UE_LOG(LogTemp, Warning, TEXT("Log: WebSocket %d isn't connected"), ConnectionId);
		}
	}
	else
	{
		UE_LOG(LogTemp, Log, TEXT("Log: WebSocketId %d doesn't exist"), ConnectionId);
	}
	return false;
}

TArray<uint8> AFortifyWebSocketConnection::ConcatenateBytesBytes(TArray<uint8> A, TArray<uint8> B)
{
	TArray<uint8> ArrayResult;

	for (int i = 0; i < A.Num(); i++)
	{
		ArrayResult.Add(A[i]);
	}

	for (int i = 0; i < B.Num(); i++)
	{
		ArrayResult.Add(B[i]);
	}

	return ArrayResult;
}

TArray<uint8> AFortifyWebSocketConnection::ConvertIntToBytes(int32 InInt)
{
	TArray<uint8> result;
	for (int i = 0; i < 4; i++)
	{
		result.Add(InInt >> i * 8);
	}
	return result;
}

TArray<uint8> AFortifyWebSocketConnection::ConvertStringToBytes(const FString& InStr)
{
	FTCHARToUTF8 Convert(*InStr);
	int BytesLength = Convert.Length();
	uint8* messageBytes = static_cast<uint8*>(FMemory::Malloc(BytesLength));
	FMemory::Memcpy(messageBytes, (uint8*)TCHAR_TO_UTF8(InStr.GetCharArray().GetData()), BytesLength);

	TArray<uint8> result;
	for (int i = 0; i < BytesLength; i++)
	{
		result.Add(messageBytes[i]);
	}

	FMemory::Free(messageBytes);	

	return result;
}

TArray<uint8> AFortifyWebSocketConnection::ConvertFloatToBytes(float InFloat)
{
	TArray<uint8> result;

	unsigned char const * p = reinterpret_cast<unsigned char const *>(&InFloat);
	for (int i = 0; i != sizeof(float); i++)
	{
		result.Add((uint8)p[i]);
	}
	return result;		
}

TArray<uint8> AFortifyWebSocketConnection::ConvertByteToBytes(uint8 InByte)
{
	TArray<uint8> result{ InByte };
	return result;
}

int32 AFortifyWebSocketConnection::MessageReadInt(TArray<uint8>& Message)
{
	if (Message.Num() < 4)
	{
		PrintToConsole("Error in the ReadInt node. Not enough bytes in the Message.", true);
		return -1;
	}

	int result;
	unsigned char byteArray[4];

	for (int i = 0; i < 4; i++)
	{
		byteArray[i] = Message[0];		
		Message.RemoveAt(0);
	}

	FMemory::Memcpy(&result, byteArray, 4);
	
	return result;
}

uint8 AFortifyWebSocketConnection::MessageReadByte(TArray<uint8>& Message)
{
	if (Message.Num() < 1)
	{
		PrintToConsole("Error in the ReadByte node. Not enough bytes in the Message.", true);
		return 255;
	}

	uint8 result = Message[0];
	Message.RemoveAt(0);
	return result;
}

bool AFortifyWebSocketConnection::MessageReadBytes(int32 NumBytes, TArray<uint8>& Message, TArray<uint8>& returnArray)
{
	for (int i = 0; i < NumBytes; i++)
	{
		if (Message.Num() >= 1)
		{
			returnArray.Add(MessageReadByte(Message));
		}
		else
		{
			return false;
		}
	}
	return true;
}

float AFortifyWebSocketConnection::MessageReadFloat(TArray<uint8>& Message)
{
	if (Message.Num() < 4)
	{
		PrintToConsole("Error in the ReadFloat node. Not enough bytes in the Message.", true);
		return -1.f;
	}

	float result;
	unsigned char byteArray[4];

	for (int i = 0; i < 4; i++)
	{
		byteArray[i] = Message[0];
		Message.RemoveAt(0);
	}

	FMemory::Memcpy(&result, byteArray, 4);

	return result;
}

FString AFortifyWebSocketConnection::MessageReadString(TArray<uint8>& Message, int32 BytesLength)
{
	if (BytesLength <= 0)
	{
		if (BytesLength < 0)
		{
			PrintToConsole("Error in the ReadString node. BytesLength isn't a positive number.", true);
		}
		
		return FString("");
	}
	if (Message.Num() < BytesLength)
	{
		PrintToConsole("Error in the ReadString node. Message isn't as long as BytesLength.", true);
		return FString("");
	}

	TArray<uint8> StringAsArray;
	StringAsArray.Reserve(BytesLength);

	for (int i = 0; i < BytesLength; i++)
	{
		StringAsArray.Add(Message[0]);
		Message.RemoveAt(0);
	}

	std::string cstr(reinterpret_cast<const char*>(StringAsArray.GetData()), StringAsArray.Num());	
	return FString(UTF8_TO_TCHAR(cstr.c_str()));
}

bool AFortifyWebSocketConnection::IsConnected(int32 ConnectionId)
{
	if (WebSockets.Contains(ConnectionId))
	{
		return WebSockets[ConnectionId]->IsConnected();
	}
	
	return false;
}

void AFortifyWebSocketConnection::PrintToConsole(FString Str, bool Error)
{
	if (Error && bPostErrorsToMessageLog)
	{
		auto messageLog = FMessageLog("WebSocket Plugin");
		messageLog.Open(EMessageSeverity::Error, true);
		messageLog.Message(EMessageSeverity::Error, FText::AsCultureInvariant(Str));
	}
	else
	{
		UE_LOG(LogTemp, Log, TEXT("Log: %s"), *Str);
	}
}