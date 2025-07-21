#include "BencodeUtils.h"
#include <iostream>

int main() {
    using namespace peerlinker::bencode;
    try {
        std::vector<BenToken> bens = decodeList("lli5ei6eee");

    } catch (std::exception& e) {
        std::cout << e.what() << std::endl;
    }

}