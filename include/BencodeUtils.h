#pragma once

#include <string>
#include <vector>
#include "ListEntry.h"

namespace peerlinker::bencode {
    // Takes in a bencoded str, e.g 4:spam, would return "spam"
    std::string decodeStr(const std::string &bencoded);

    // Takes in a bencoded int, e.g i56e, returns 56.
    // upto a maximum of a 64 bit int
    int64_t decodeInt(const std::string &bencoded);

    // Takes in a bencoded list, e.g l4:spame, returns ["spam"]
    std::vector<ListEntry> decodeList(const std::string &bencoded);

    // In a dictionary, find the corresponding value of a key.
    std::string findKey(std::string dictionary, std::string keyName);

    bencodeType determineType(const std::string &bencoded);
}
