import struct

# frame_id (uint32), chunk_id (uint16), total_chunks (uint16)
HEADER = struct.Struct("!IHH")

def pack_packet(frame_id, chunk_id, total_chunks, payload):
    return HEADER.pack(frame_id, chunk_id, total_chunks) + payload

def unpack_packet(data):
    header_size = HEADER.size
    frame_id, chunk_id, total_chunks = HEADER.unpack(data[:header_size])
    payload = data[header_size:]
    return frame_id, chunk_id, total_chunks, payload
