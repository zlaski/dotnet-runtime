project(mscoree)
add_definitions(-DFEATURE_CORECLR)

include_directories("../../inc")

if(FEATURE_GDBJIT)
   add_definitions(-DFEATURE_GDBJIT)
endif(FEATURE_GDBJIT)

set(CLR_SOURCES
    dbgshim.cpp
    mscoree.cpp
    exports.cpp
)

if(CLR_CMAKE_TARGET_WIN32)
list(APPEND CLR_SOURCES
    ${CLR_SRC_NATIVE_DIR}/libs/Common/delayloadhook_windows.cpp
    Native.rc
)

set (DEF_SOURCES
  mscorwks_ntdef.src
)
else()
set (DEF_SOURCES
  mscorwks_unixexports.src
)
if(FEATURE_GDBJIT)
  list(APPEND DEF_SOURCES
    gdbjit_unixexports.src
  )
endif(FEATURE_GDBJIT)
endif(CLR_CMAKE_TARGET_WIN32)

convert_to_absolute_path(DEF_SOURCES ${DEF_SOURCES})
convert_to_absolute_path(CLR_SOURCES ${CLR_SOURCES})

# we need to define this library before adding .def fiels to sources.
add_library_clr(mscoree_static
    STATIC
    ${CLR_SOURCES}
)

if (CLR_CMAKE_TARGET_WIN32)
    preprocess_file(${DEF_SOURCES} ${CMAKE_CURRENT_BINARY_DIR}/mscoree.def)

    list(APPEND CLR_SOURCES ${CMAKE_CURRENT_BINARY_DIR}/mscoree.def)
endif (CLR_CMAKE_TARGET_WIN32)

if (CLR_CMAKE_HOST_WIN32)
    set (DEF_FILE  ${CMAKE_CURRENT_BINARY_DIR}/mscoree.def)
    preprocess_file(${DEF_SOURCES} ${DEF_FILE})

    list(APPEND CLR_SOURCES ${CMAKE_CURRENT_BINARY_DIR}/mscoree.def)

    # Incremental linking results in the linker inserting extra padding and routing function calls via thunks that can break the
    # invariants (e.g. size of region between Jit_PatchedCodeLast-Jit_PatchCodeStart needs to fit in a page).
    add_linker_flag("/INCREMENTAL:NO")

    # Delay load libraries required for WinRT as that is not supported on all platforms
    add_linker_flag("/DELAYLOAD:api-ms-win-core-winrt-l1-1-0.dll")

    # Delay load version.dll so that we can specify how to search when loading it as it is not part of Windows' known DLLs
    add_linker_flag("/DELAYLOAD:version.dll")

    # No library groups for Win32
    set(START_LIBRARY_GROUP)
    set(END_LIBRARY_GROUP)

else(CLR_CMAKE_HOST_WIN32)
    set(EXPORTS_FILE ${CMAKE_CURRENT_BINARY_DIR}/mscoree.exports)
    generate_exports_file(${DEF_SOURCES} ${EXPORTS_FILE})

    if(CLR_CMAKE_TARGET_LINUX OR CLR_CMAKE_TARGET_FREEBSD OR CLR_CMAKE_TARGET_NETBSD OR CLR_CMAKE_TARGET_SUNOS OR CLR_CMAKE_HOST_HAIKU)
        # This option is necessary to ensure that the overloaded delete operator defined inside
        # of the utilcode will be used instead of the standard library delete operator.
        add_linker_flag("-Wl,-Bsymbolic")

        # The following linked options can be inserted into the linker libraries list to
        # ensure proper resolving of circular references between a subset of the libraries.
        set(START_LIBRARY_GROUP -Wl,--start-group)
        set(END_LIBRARY_GROUP -Wl,--end-group)

        # These options are used to force every object to be included even if it's unused.
        set(START_WHOLE_ARCHIVE -Wl,--whole-archive)
        set(END_WHOLE_ARCHIVE -Wl,--no-whole-archive)
    endif(CLR_CMAKE_TARGET_LINUX OR CLR_CMAKE_TARGET_FREEBSD OR CLR_CMAKE_TARGET_NETBSD OR CLR_CMAKE_TARGET_SUNOS OR CLR_CMAKE_HOST_HAIKU)

    if(CLR_CMAKE_TARGET_OSX)
        # These options are used to force every object to be included even if it's unused.
        set(START_WHOLE_ARCHIVE -force_load)
        set(END_WHOLE_ARCHIVE )
    endif(CLR_CMAKE_TARGET_OSX)

    set_exports_linker_option(${EXPORTS_FILE})

endif (CLR_CMAKE_HOST_WIN32)

add_definitions(-DFX_VER_INTERNALNAME_STR=mscoree.dll)

add_library_clr(mscoree
    SHARED
    ${CLR_SOURCES}
)

# add the install targets
install_clr(TARGETS mscoree DESTINATIONS . sharedFramework COMPONENT runtime)
if(CLR_CMAKE_HOST_MACCATALYST OR CLR_CMAKE_HOST_IOS)
    install_clr(TARGETS mscoree_static DESTINATIONS . sharedFramework COMPONENT runtime)
endif()
