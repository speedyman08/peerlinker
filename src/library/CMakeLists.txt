add_library(peerlinker STATIC
        BencodeUtils.cpp
)

target_link_libraries(
        peerlinker
        PUBLIC
        asio::asio
        cpr::cpr
        fmt::fmt
        magic_enum::magic_enum
)

target_include_directories(peerlinker
        PUBLIC
        $<BUILD_INTERFACE:${PROJECT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:${CMAKE_INSTALL_PREFIX}/include/peerlinker>
        PRIVATE include
)