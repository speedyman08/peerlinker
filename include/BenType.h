#pragma once

namespace peerlinker::bencode {
    enum bencodeType {
        String,
        Integer,
        List,
        Dictionary,
        None
    };
}