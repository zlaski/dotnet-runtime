include_directories("../../inc")

if(FEATURE_GDBJIT)
   add_definitions(-DFEATURE_GDBJIT)
endif(FEATURE_GDBJIT)

set(CLR_SOURCES
    mscoree.cpp
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
add_subdirectory(coreclr)
