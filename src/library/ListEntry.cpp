#include "ListEntry.h"

std::string ListEntry::asString()  {
    if (std::holds_alternative<std::string>(benValue)) {
        return std::get<std::string>(benValue);
    }
    throw std::exception("BenValue is not a string");

}

int64_t ListEntry::asInt() {
    if (std::holds_alternative<int64_t>(benValue)) {
        return std::get<int64_t>(benValue);
    }
    throw std::exception("BenValue is not an int");
}

std::vector<ListEntry> ListEntry::asList() {
    if (std::holds_alternative<std::vector<ListEntry>>(benValue)) {
        return std::get<std::vector<ListEntry>>(benValue);
    }
    throw std::exception("BenValue is not an int");
}
