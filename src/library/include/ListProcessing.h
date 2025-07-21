#pragma once

#include <string>
#include <vector>
#include "BenType.h"
#include "BenToken.h"

namespace peerlinker::bencode {
    void createListElement(bencodeType benType, const std::string& substr, std::vector<BenToken>& children);
}
