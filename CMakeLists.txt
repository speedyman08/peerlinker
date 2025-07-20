cmake_minimum_required(VERSION 3.31)
project(
        peerlinker
)

set(CMAKE_CXX_STANDARD 20)

option(WITH_TESTER "Build library tester" OFF)

if(VCPKG)
    list(APPEND CMAKE_PREFIX_PATH ${CMAKE_CURRENT_SOURCE_DIR}/vcpkg_installed/${VCPKG_TRIPLET})
endif()

find_package(curl CONFIG REQUIRED)
find_package(cpr CONFIG REQUIRED)
find_package(asio CONFIG REQUIRED)

add_subdirectory(src/library)

if(WITH_TESTER)
    add_subdirectory(src/tester)
endif()
