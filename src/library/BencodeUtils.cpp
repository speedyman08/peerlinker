#include "BencodeUtils.h"
#include <fstream>
#include <iostream>
#include <sstream>
#include <unordered_map>

#include "Detection.h"
#include "ListProcessing.h"

std::string streamReadUntil(std::istringstream &strStream, char delim) {
    std::string item{};
    while (strStream.get() != delim) {
        strStream.unget();
        item.append(1, strStream.get());
    }
    return item;
}

namespace peerlinker::bencode {
    std::string decodeStr(const std::string &bencode) {
        std::string result{};

        std::istringstream strStream{bencode};

        if (bencode.find(':') == std::string::npos) {
            throw std::invalid_argument("Invalid bencode string format (no colon)");
        }

        std::string digits = streamReadUntil(strStream, ':');

        int strLength = 0;

        try {
            strLength = std::stoi(digits);
        } catch (const std::invalid_argument &) {
            throw std::invalid_argument("Invalid bencode string format (invalid string length)");
        }

        if (strLength != bencode.substr(digits.length() + 1).length()) {
            throw std::invalid_argument("Invalid bencode string format (invalid length)");
        }

        auto buf = new char[strLength];

        strStream.read(buf, strLength);
        result.append(buf, strLength);

        delete[] buf;
        return result;
    }

    int64_t decodeInt(const std::string &bencode) {
        std::istringstream strStream{bencode};


        if (strStream.get() != 'i') {
            throw std::invalid_argument("Invalid bencode int format (does not begin with i)");
        }

        if (bencode.find('e') == std::string::npos) {
            throw std::invalid_argument("Invalid bencode int format (does not contain the ending delimeter, e)");
        }

        const std::string digits = streamReadUntil(strStream, 'e');

        try {
            int64_t result = std::stoi(digits);
            return result;
        } catch (std::invalid_argument &) {
            throw std::invalid_argument("Invalid bencode int format (invalid num inbetween i and e)");
        }
    }

    std::vector<BenToken> decodeSequentialElements(std::string&& bencoded) {
        std::vector<BenToken> children {};

        int skipCount = 0;

        for (int i = 1; i <= bencoded.length(); i++) {
            const int tailOffset = bencoded.length() - i;
            std::string currentSubstr = bencoded.substr(tailOffset);

            if (currentSubstr.front() == 'e') {
                skipCount++;
            }

            // skip until the needed delim appears
            if ((currentSubstr.front() == 'l' || currentSubstr.front() == 'i') && skipCount >= 1) {
                skipCount--;
                if (skipCount != 0) {
                    continue;
                }
            }

            auto type = determineType(currentSubstr);

            if (type == None) continue;

            // success
            createListElement(type, currentSubstr, children);

            bencoded.erase(bencoded.length() - currentSubstr.length(), currentSubstr.length());
            if (bencoded.empty()) break;

            skipCount = 0;
            i = 0;
        }

        std::reverse(children.begin(), children.end());
        return children;
    }

    std::vector<BenToken> decodeList(std::string bencoded) {
        if (*bencoded.begin() != 'l' || bencoded.back() != 'e') {
            throw std::invalid_argument("Invalid bencode list format (does not begin with l or e)");
        }

        // strip l and e
        bencoded.erase(bencoded.begin());
        bencoded.pop_back();

        return decodeSequentialElements(std::move(bencoded));
    }

    std::unordered_map<std::string, BenToken> decodeDict(std::string bencoded) {
        // strip identifying chars
        bencoded.erase(bencoded.begin());
        bencoded.pop_back();

        std::vector<BenToken> children = decodeSequentialElements(std::move(bencoded));
        if (children.size() < 2 || children.size() % 2 != 0) throw std::invalid_argument("Invalid bencode dict format");

        std::unordered_map<std::string, BenToken> accumDict {};

        for (int i = 0; i < children.size(); i += 2) {
            auto key = children.at(i).expect<std::string>();
            BenToken val = children.at(i + 1);

            std::pair curPair {key, val};

            accumDict.insert(curPair);
        }

        return accumDict;
    }

    void createListElement(const bencodeType benType, const std::string &bencoded,
                           std::vector<BenToken> &children) {
        BenToken entry { benType };

        switch (benType) {
            case String: {
                entry.benValue = decodeStr(bencoded);
                break;
            }
            case Integer: {
                entry.benValue = decodeInt(bencoded);
                break;
            }
            case List: {
                entry.benValue = decodeList(bencoded);
                break;
            }

            case Dictionary: {
                entry.benValue = decodeDict(bencoded);
            }
            default: ;
        }

        children.push_back(entry);
    }

    bencodeType determineType(const std::string &bencoded) {
        if (isString(bencoded)) {
            return String;
        }

        if (isInt(bencoded)) {
            return Integer;
        }

        if (isList(bencoded)) {
            return List;
        }

        if (isDict(bencoded)) {
            return Dictionary;
        }

        return None;
    }

    bool isString(const std::string &bencoded) {
        std::istringstream strStream{bencoded};

        if (!std::isdigit(*bencoded.begin())) {
            return false;
        }

        if (bencoded.find(':') == std::string::npos) {
            return false;
        }

        std::string digits = streamReadUntil(strStream, ':');
        std::string afterColon = bencoded.substr(digits.length() + 1);

        if (afterColon.length() != std::stoi(digits)) {
            return false;
        }

        return true;
    }

    bool isInt(const std::string &bencoded) {
        if (bencoded.front() == 'i' && bencoded.back() == 'e') return true;

        return false;
    }

    bool isList(const std::string &bencoded) {
        if (bencoded.front() == 'l' && bencoded.back() == 'e') return true;

        return false;
    }

    bool isDict(const std::string &bencoded) {
        if (bencoded.front() == 'd' && bencoded.back() == 'e') return true;

        return false;
    }
}
