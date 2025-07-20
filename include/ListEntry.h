#pragma once

#include <variant>
#include <string>
#include <vector>

#include "BenType.h"

using namespace peerlinker::bencode;

struct ListEntry {
    bencodeType benType;

    std::variant<int64_t, std::string, std::vector<ListEntry>> benValue;

    std::string asString();

    int64_t asInt();

    std::vector<ListEntry> asList();
};
