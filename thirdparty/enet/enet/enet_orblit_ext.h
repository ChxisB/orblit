/**************************************************************************/
/*  enet_orblit_ext.h                                                     */

/**
 @ *file  enet_orblit_ext.h
 @brief ENet Orblit extensions header
 */

#ifndef __ENET_ORBLIT_EXT_H__
#define __ENET_ORBLIT_EXT_H__

/** Sets the host field in the address parameter from ip struct.
    @param address destination to store resolved address
    @param ip the ip struct to read from
    @param size the size of the ip struct.
    @retval 0 on success
    @retval != 0 on failure
    @returns the address of the given ip in address on success.
*/
ENET_API void enet_address_set_ip(ENetAddress * address, const uint8_t * ip, size_t size);

ENET_API int enet_host_dtls_server_setup (ENetHost *, void *);
ENET_API int enet_host_dtls_client_setup (ENetHost *, const char *, void *);
ENET_API void enet_host_refuse_new_connections (ENetHost *, int);
ENET_API void enet_peer_socket_bind (ENetPeer *);
ENET_API void enet_peer_socket_destroy (ENetPeer *);

#endif // __ENET_ORBLIT_EXT_H__
