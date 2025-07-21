add_library(peerlinker STATIC
        BencodeUtils.cpp
)

target_link_libraries(
        peerlinker
        PUBLIC
        asio::asio
        cpr::cpr
)

target_include_directories(peerlinker PUBLIC ../../include)
target_include_directories(peerlinker PRIVATE include)