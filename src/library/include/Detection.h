#pragma once
#include <string>

namespace peerlinker::bencode {
    bool isString(const std::string &bencoded, std::istringstream &strStream);
    bool isInt (const std::string &bencoded);
    bool isList(const std::string &bencoded);
}
