#pragma once
#include <windows.h>
// #include "backends/p2p.h"

#ifndef _HINSTLIB
#define _HINSTLIB
extern HINSTANCE hinstLib;
#endif

// Forward delaration
class UdpMsg;
class Peer2PeerBackend{
public:
	virtual void Peer2PeerBackend::OnMsg(sockaddr_in& from, UdpMsg* msg, int len);
};
