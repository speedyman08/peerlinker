add_executable(peerlinkertest
    main.cpp
)
# Use public headers

target_include_directories(peerlinkertest PRIVATE ../../include)
target_link_libraries(peerlinkertest
        peerlinker
)