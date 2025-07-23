#pragma once
#include <fmt/format.h>
#include <fmt/ranges.h>
#include "BenToken.h"

template <>
struct fmt::formatter<BenToken> : formatter<std::string_view> {
    using base = formatter<std::string_view>;
    using base::parse;

    static std::string formatList(const BenToken& tk) {
        const auto vec = tk.expect<std::vector<BenToken>>();
        std::string str;
        int entry = 0;
        for (auto& token : vec) {
            switch (token.benType) {
                case Integer: {
                    str.append(fmt::format("(Entry {}) {}", entry, token.expect<int64_t>()));
                    break;
                }
                case String: {
                    str.append(fmt::format("(Entry {}) {}", entry, token.expect<std::string>()));
                    break;
                }
                case List: {
                    str.append(formatList(token));
                    break;
                }
                default: continue;
            }
            entry++;
        }
        return str;
    }

    template <typename FmtContext>
    auto format (const BenToken &tk, FmtContext& ctx) const {
        std::string str = "BenToken";

        switch (tk.benType) {
            case Integer: {
                str = fmt::format("{}", tk.expect<int64_t>());
                break;
            }
            case String: {
                str = fmt::format("{}", tk.expect<std::string>());
                break;
            }
            case List: {
                str = formatList(tk);
                break;
            }
            default: ;
        }

        return formatter<string_view>::format(str, ctx);
    }
};
