#include "BencodeUtils.h"
#include <iostream>

int main() {
    using namespace peerlinker::bencode;
    try {
        std::unordered_map<std::string, BenToken> hash = decodeDict("d6:numberi64ee");

        auto num = hash.at("number").expect<int64_t>();

        std::cout << num << std::endl;
    } catch (std::exception& e) {
        std::cout << e.what() << std::endl;
    }

}