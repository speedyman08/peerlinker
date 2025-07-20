#include "BencodeUtils.h"
#include <iostream>

int main() {
    using namespace peerlinker::bencode;
    try {
        std::vector<ListEntry> benList = decodeList("l5:hellol3:gayee");
        // must be like ['hello', ['gay']]

        // lol holy shit
        if (benList.at(0).asString() == "hello"
            && benList.at(1).asList().at(0).asString() == "gay"
        ) {
            std::cout << "test passes\n";
        }
    } catch (std::exception& e) {
        std::cout << e.what() << std::endl;
    }

}