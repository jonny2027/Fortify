// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "Trace/Config.h"

#if TRACE_PRIVATE_MINIMAL_ENABLED

namespace UE {
namespace Trace {
namespace Private {

////////////////////////////////////////////////////////////////////////////////
UPTRINT	ThreadCreate(const ANSICHAR* Name, void (*Entry)());
void	ThreadSleep(uint32 Milliseconds);
void	ThreadJoin(UPTRINT Handle);
void	ThreadDestroy(UPTRINT Handle);

////////////////////////////////////////////////////////////////////////////////
uint64				TimeGetFrequency();
TRACELOG_API uint64	TimeGetTimestamp();

////////////////////////////////////////////////////////////////////////////////
UPTRINT	TcpSocketConnect(const ANSICHAR* Host, uint16 Port);
UPTRINT	TcpSocketListen(uint16 Port);
int32	TcpSocketAccept(UPTRINT Socket, UPTRINT& Out);
bool	TcpSocketHasData(UPTRINT Socket);

////////////////////////////////////////////////////////////////////////////////
int32	IoRead(UPTRINT Handle, void* Data, uint32 Size);
bool	IoWrite(UPTRINT Handle, const void* Data, uint32 Size);
void	IoClose(UPTRINT Handle);

////////////////////////////////////////////////////////////////////////////////
UPTRINT	FileOpen(const ANSICHAR* Path);
	
////////////////////////////////////////////////////////////////////////////////
int32	GetLastErrorCode();
bool	GetErrorMessage(char* OutBuffer, uint32 BufferSize, int32 ErrorCode);	

////////////////////////////////////////////////////////////////////////////////
#if !defined(TRACE_PRIVATE_HAS_THROTTLE)
#	define	TRACE_PRIVATE_HAS_THROTTLE	0
#endif

#if TRACE_PRIVATE_HAS_THROTTLE
#	define	THROTTLE_IMPL(x)	;
#	define	THROTTLE_INL
#else
#	define	THROTTLE_IMPL(x)	{x}
#	define	THROTTLE_INL		inline
#endif

THROTTLE_INL int32	ThreadThrottle()		THROTTLE_IMPL(return 0;)
THROTTLE_INL void	ThreadUnthrottle(int32)	THROTTLE_IMPL()

#undef THROTTLE_INL
#undef THROTTLE_IMPL

} // namespace Private
} // namespace Trace
} // namespace UE

#endif // TRACE_PRIVATE_MINIMAL_ENABLED