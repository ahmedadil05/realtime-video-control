import struct

# sequence (uint32), timestamp (float64), payload size (uint16)
HEADER = struct.Struct("!IdH")

def pack_header(seq, timestamp, size):
    return HEADER.pack(seq, timestamp, size)

def unpack_header(data):
    return HEADER.unpack(data)
