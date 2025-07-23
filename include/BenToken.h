#pragma once

#include <variant>
#include <string>
#include <vector>
#include <unordered_map>
#include <BenType.h>


using namespace peerlinker::bencode;
struct BenToken;

typedef std::variant<int64_t, std::string, std::vector<BenToken>, std::unordered_map<std::string, BenToken>> possibleTypes;

struct BenToken {
    bencodeType benType;
    possibleTypes benValue;

    explicit BenToken(bencodeType benType) : benType(benType) {};

    template<typename T>
    T expect() const {
        static_assert(
            (   std::is_same_v<T, std::string> ||
                std::is_same_v<T, std::int64_t> ||
                std::is_same_v<T, std::vector<BenToken>>
            ),
            "T must either be a int64_t, string, vector of ListEntries"
        );

        // still check for runtime
        if (std::holds_alternative<T>(benValue)) {
            return std::get<T>(benValue);
        }

        throw std::bad_variant_access();
    }
};
