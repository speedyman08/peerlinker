#pragma once

#include <string>
#include <vector>
#include "BenType.h"
#include "ListEntry.h"

namespace peerlinker::bencode {
    void createListElement(bencodeType benType, const std::string& substr, std::vector<ListEntry>& children);
}
