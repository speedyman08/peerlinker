#include "BencodeUtils.h"
#include <fstream>
#include <iostream>
#include <sstream>

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

    std::vector<ListEntry> decodeList(const std::string &bencoded) {
        std::string copy = bencoded;
        if (*bencoded.begin() != 'l' || bencoded.back() != 'e') {
            throw std::invalid_argument("Invalid bencode list format (does not begin with l or e)");
        }
        std::vector<ListEntry> children;

        // strip l and e
        copy.erase(copy.begin());
        copy.pop_back();

        for (int i = 1; i <= copy.length(); i++) {
            std::string currentSubstr = copy.substr(copy.length() - i);
            auto type = determineType(currentSubstr);

            if (type == None) continue;

            switch (type) {
                case String: {
                    createListElement(String, currentSubstr, children);
                    break;
                }
                case Integer: {
                    createListElement(Integer, currentSubstr, children);
                    break;
                }

                case List: {
                    createListElement(List, currentSubstr, children);
                    break;
                }
                default: ;
            }

            copy.erase(copy.length() - currentSubstr.length(), currentSubstr.length());
            if (copy.empty()) break;
            i = 0;
        }

        std::reverse(children.begin(), children.end());
        return children;
    }

    void createListElement(const bencodeType benType, const std::string &bencoded, std::vector<ListEntry> &children) {
        ListEntry entry{
            .benType = benType
        };

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
            default: ;
        }

        children.push_back(entry);
    }

    // TODO: ints
    bencodeType determineType(const std::string &bencoded) {
        std::istringstream strStream{bencoded};

        if (isString(bencoded, strStream)) {
            return String;
        }

        if (isInt(bencoded)) {
            return Integer;
        }

        if (isList(bencoded)) {
            return List;
        }

        return None;
    }

    bool isString(const std::string &bencoded, std::istringstream &strStream) {
        if (!std::isdigit(*bencoded.begin())) {
            return false;
        }

        // now check for correctness, else return None
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
        // end is an iterator, really a pointer, to the char one AFTER the end
        if (bencoded.front() == 'i' && bencoded.back() == 'e') return true;

        return false;
    }

    bool isList(const std::string &bencoded) {
        if (bencoded.front() == 'l' && bencoded.back() == 'e') return true;

        return false;
    }
}
